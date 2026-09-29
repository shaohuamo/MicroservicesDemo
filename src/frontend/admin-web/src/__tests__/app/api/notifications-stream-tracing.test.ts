// @vitest-environment node

import { context, ROOT_CONTEXT, trace, type SpanContext } from "@opentelemetry/api";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import type { NextRequest } from "next/server";

const mocks = vi.hoisted(() => ({
  auth: vi.fn(),
  registerConnection: vi.fn(),
  fetchReplayPage: vi.fn(),
}));

vi.mock("@/auth", () => ({ auth: mocks.auth }));
vi.mock("@/lib/notifications/server-hub", () => ({
  registerNotificationConnection: mocks.registerConnection,
}));
vi.mock("@/lib/notifications/server-gateway", () => ({
  fetchNotificationReplayPage: mocks.fetchReplayPage,
  NotificationGatewayError: class NotificationGatewayError extends Error {},
}));

import { GET } from "@/app/api/notifications/stream/route";
import { initializeServerTelemetry } from "@/lib/notifications/server-otel";

describe("notification stream tracing", () => {
  beforeAll(() => {
    initializeServerTelemetry();
  });

  beforeEach(() => {
    mocks.auth.mockReset();
    mocks.registerConnection.mockReset();
    mocks.fetchReplayPage.mockReset();
  });

  it("keeps refresh and replay under the active Next.js request even with an incoming traceparent", async () => {
    const activeSpans: string[] = [];
    let setupParent: SpanContext | undefined;
    let setupTraceId = "";
    mocks.auth.mockImplementation(async (handler?: (request: object) => Promise<Response>) => {
      if (!handler) {
        const setupSpan = trace.getActiveSpan();
        activeSpans.push(setupSpan?.spanContext().spanId ?? "");
        setupParent = (setupSpan as typeof setupSpan & { parentSpanContext?: SpanContext })
          ?.parentSpanContext;
        setupTraceId = setupSpan?.spanContext().traceId ?? "";
        return { userId: "user-1" };
      }
      return async () => {
        // The Auth.js JWT callback performs the Redis refresh work here.
        activeSpans.push(trace.getActiveSpan()?.spanContext().spanId ?? "");
        return handler({ auth: { userId: "user-1", accessToken: "access-token" } });
      };
    });
    mocks.registerConnection.mockResolvedValue({
      close: vi.fn(async () => undefined),
      refreshPresence: vi.fn(async () => undefined),
      activateLiveDelivery: () => [],
    });
    mocks.fetchReplayPage.mockImplementation(async () => {
      activeSpans.push(trace.getActiveSpan()?.spanContext().spanId ?? "");
      return { items: [], nextAfterSequence: null, watermark: 0 };
    });

    const parent = trace.getTracer("notifications-stream-tracing.test").startSpan("next-route");
    const response = await context.with(trace.setSpan(ROOT_CONTEXT, parent), () =>
      GET(new Request("http://localhost/api/notifications/stream", {
        headers: {
          traceparent: "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01",
        },
      }) as NextRequest));
    parent.end();

    expect(activeSpans).toHaveLength(3);
    expect(setupParent?.spanId).toBe(parent.spanContext().spanId);
    expect(setupTraceId).toBe(parent.spanContext().traceId);
    expect(activeSpans[0]).not.toBe("");
    expect(activeSpans[0]).not.toBe(parent.spanContext().spanId);
    expect(activeSpans[1]).toBe(activeSpans[0]);
    expect(activeSpans[2]).toBe(activeSpans[0]);
    await response?.body?.cancel();
  });
});
