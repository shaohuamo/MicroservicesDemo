// @vitest-environment node

import { context, ROOT_CONTEXT, trace, type SpanContext } from "@opentelemetry/api";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { NextRequest } from "next/server";
import { getAuthTracer, initializeServerTelemetry } from "@/lib/notifications/server-otel";

const mocks = vi.hoisted(() => ({
  auth: vi.fn(),
  registerConnection: vi.fn(),
}));

vi.mock("server-only", () => ({}));
vi.mock("@/auth", () => ({ auth: mocks.auth }));
vi.mock("@/lib/auth/gateway-session-proof", () => ({
  getRefreshTokenRecordId: vi.fn(async () => "11111111-1111-1111-1111-111111111111"),
  applyGatewaySessionProof: vi.fn(),
}));
vi.mock("@/lib/notifications/server-hub", () => ({
  registerNotificationConnection: mocks.registerConnection,
}));

import { GET } from "@/app/api/notifications/stream/route";

describe("notification stream gateway tracing", () => {
  beforeAll(() => {
    initializeServerTelemetry();
  });

  afterEach(() => {
    mocks.auth.mockReset();
    mocks.registerConnection.mockReset();
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
  });

  it("propagates the request trace through auth refresh and replay fetch", async () => {
    vi.stubEnv("API_GATEWAY_INTERNAL_URL", "http://apigateway:8080");
    const connection = {
      close: vi.fn(async () => undefined),
      refreshPresence: vi.fn(async () => undefined),
      isRealtimeAvailable: () => true,
      waitForRealtimeReady: vi.fn(async () => true),
      getDisconnectGeneration: () => 0,
      activateLiveDelivery: () => [],
    };
    mocks.registerConnection.mockResolvedValue(connection);

    const refreshParents: SpanContext[] = [];
    mocks.auth.mockImplementation(async (handler?: (request: object) => Promise<Response>) => {
      await getAuthTracer().startActiveSpan("redis GET refresh.result", async (span) => {
        const parent = (span as typeof span & { parentSpanContext?: SpanContext }).parentSpanContext;
        if (parent) refreshParents.push(parent);
        span.end();
      });

      if (!handler) return { userId: "user-1" };
      return async () => handler({ auth: { userId: "user-1", accessToken: "access-token" } });
    });

    let gatewayParent: SpanContext | undefined;
    let gatewaySpanId = "";
    let gatewayTraceparent = "";
    vi.stubGlobal("fetch", vi.fn(async (_url: string, init?: RequestInit) => {
      const span = trace.getActiveSpan();
      gatewayParent = (span as typeof span & { parentSpanContext?: SpanContext } | undefined)
        ?.parentSpanContext;
      gatewaySpanId = span?.spanContext().spanId ?? "";
      gatewayTraceparent = new Headers(init?.headers).get("traceparent") ?? "";
      return Response.json({ items: [], nextCursor: null, watermark: 0 });
    }));

    const nextSpan = getAuthTracer().startSpan("next request");
    let response: Response;
    try {
      response = await context.with(trace.setSpan(ROOT_CONTEXT, nextSpan), async () => {
        const result = await GET(new Request("http://localhost/api/notifications/stream", {
          headers: {
            traceparent: "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01",
          },
        }) as NextRequest);
        if (!result) throw new Error("The SSE route did not return a response.");
        return result;
      });
    } finally {
      nextSpan.end();
    }

    expect(response.status).toBe(200);
    expect(refreshParents).toHaveLength(2);
    expect(refreshParents[0].spanId).toBe(refreshParents[1].spanId);
    expect(gatewayParent?.spanId).toBe(refreshParents[0].spanId);
    expect(gatewayTraceparent).toBe(
      `00-${nextSpan.spanContext().traceId}-${gatewaySpanId}-01`,
    );
    await response.body?.cancel();
    expect(connection.close).toHaveBeenCalled();
  });
});
