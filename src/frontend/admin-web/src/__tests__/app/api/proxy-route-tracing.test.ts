// @vitest-environment node

import { context, ROOT_CONTEXT, SpanStatusCode, trace, type SpanContext } from "@opentelemetry/api";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { NextRequest } from "next/server";
import { getAuthTracer, initializeServerTelemetry } from "@/lib/notifications/server-otel";

type RouteContext = { params: Promise<{ path: string[] }> };

const mocks = vi.hoisted(() => ({ auth: vi.fn() }));
vi.mock("@/auth", () => ({ auth: mocks.auth }));

import { GET } from "@/app/api/[...path]/route";

describe("admin API proxy tracing", () => {
  beforeAll(() => {
    initializeServerTelemetry();
  });

  afterEach(() => {
    mocks.auth.mockReset();
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
  });

  it("keeps auth refresh and gateway work under the active Next.js request", async () => {
    vi.stubEnv("API_GATEWAY_INTERNAL_URL", "http://apigateway:8080");
    let refreshParent: SpanContext | undefined;
    let refreshTraceId = "";
    let gatewayParent: SpanContext | undefined;
    let gatewaySpanId = "";
    let gatewayTraceparent = "";

    mocks.auth.mockImplementation(async (
      handler: (request: NextRequest & { auth: { accessToken: string } }, route: RouteContext) => Promise<Response>,
    ) => async (request: NextRequest, route: RouteContext) => {
      await getAuthTracer().startActiveSpan("redis GET refresh.result", async (span) => {
        refreshParent = (span as typeof span & { parentSpanContext?: SpanContext }).parentSpanContext;
        refreshTraceId = span.spanContext().traceId;
        span.end();
      });
      return handler(Object.assign(request, { auth: { accessToken: "access-token" } }), route);
    });

    vi.stubGlobal("fetch", vi.fn(async (_url: URL, init?: RequestInit) => {
      const span = trace.getActiveSpan();
      gatewayParent = (span as typeof span & { parentSpanContext?: SpanContext } | undefined)
        ?.parentSpanContext;
      gatewaySpanId = span?.spanContext().spanId ?? "";
      gatewayTraceparent = new Headers(init?.headers).get("traceparent") ?? "";
      return new Response("[]", { status: 200, headers: { "content-type": "application/json" } });
    }));

    const request = {
      method: "GET",
      headers: new Headers({
        traceparent: "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01",
      }),
      nextUrl: new URL("http://localhost/api/products"),
    } as NextRequest;
    const route = { params: Promise.resolve({ path: ["products"] }) };
    const nextSpan = getAuthTracer().startSpan("next request");
    let response: Response;
    try {
      response = await context.with(trace.setSpan(ROOT_CONTEXT, nextSpan), () => GET(request, route));
    } finally {
      nextSpan.end();
    }

    expect(response.status).toBe(200);
    expect(refreshParent?.spanId).toBe(nextSpan.spanContext().spanId);
    expect(refreshTraceId).toBe(nextSpan.spanContext().traceId);
    expect(gatewayParent?.spanId).toBe(nextSpan.spanContext().spanId);
    expect(gatewayTraceparent).toBe(
      `00-${nextSpan.spanContext().traceId}-${gatewaySpanId}-01`,
    );
  });

  it("preserves the request trace and marks a failed gateway span", async () => {
    vi.stubEnv("API_GATEWAY_INTERNAL_URL", "http://apigateway:8080");
    mocks.auth.mockImplementation(async (
      handler: (request: NextRequest & { auth: { accessToken: string } }, route: RouteContext) => Promise<Response>,
    ) => async (request: NextRequest, route: RouteContext) =>
      handler(Object.assign(request, { auth: { accessToken: "access-token" } }), route));

    let gatewaySpan: (ReturnType<typeof trace.getActiveSpan> & {
      name: string;
      status: { code: SpanStatusCode };
      parentSpanContext?: SpanContext;
    }) | undefined;
    vi.stubGlobal("fetch", vi.fn(async () => {
      gatewaySpan = trace.getActiveSpan() as typeof gatewaySpan;
      return new Response("unavailable", { status: 503 });
    }));

    const nextSpan = getAuthTracer().startSpan("next request");
    const response = await context.with(trace.setSpan(ROOT_CONTEXT, nextSpan), () =>
      GET({
        method: "GET",
        headers: new Headers(),
        nextUrl: new URL("http://localhost/api/notifications"),
      } as NextRequest, { params: Promise.resolve({ path: ["notifications"] }) }));
    nextSpan.end();

    expect(response.status).toBe(503);
    expect(gatewaySpan?.name).toBe("gateway GET /gateway/notifications");
    expect(gatewaySpan?.status.code).toBe(SpanStatusCode.ERROR);
    expect(gatewaySpan?.parentSpanContext?.spanId).toBe(nextSpan.spanContext().spanId);
    expect(gatewaySpan?.spanContext().traceId).toBe(nextSpan.spanContext().traceId);
  });

  it("returns an auth failure within the request trace without calling the gateway", async () => {
    vi.stubEnv("API_GATEWAY_INTERNAL_URL", "http://apigateway:8080");
    let authSpanId = "";
    mocks.auth.mockImplementation(async () => async () => {
      authSpanId = trace.getActiveSpan()?.spanContext().spanId ?? "";
      return Response.json({ message: "Unauthorized" }, { status: 401 });
    });
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const nextSpan = getAuthTracer().startSpan("next request");
    const response = await context.with(trace.setSpan(ROOT_CONTEXT, nextSpan), () =>
      GET({
        method: "GET",
        headers: new Headers(),
        nextUrl: new URL("http://localhost/api/products"),
      } as NextRequest, { params: Promise.resolve({ path: ["products"] }) }));
    nextSpan.end();

    expect(response.status).toBe(401);
    expect(authSpanId).toBe(nextSpan.spanContext().spanId);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
