// @vitest-environment node

import {
  context,
  createTraceState,
  ROOT_CONTEXT,
  trace,
} from "@opentelemetry/api";
import { beforeAll, describe, expect, it } from "vitest";
import {
  getServerTracer,
  initializeServerTelemetry,
  resolveRequestTraceContext,
} from "@/lib/notifications/server-otel";

const incomingTraceparent = "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01";

describe("request trace context resolution", () => {
  beforeAll(() => {
    initializeServerTelemetry();
  });

  it("keeps the active request span when an incoming traceparent is also present", () => {
    const requestSpan = getServerTracer().startSpan("next request");

    try {
      context.with(trace.setSpan(ROOT_CONTEXT, requestSpan), () => {
        const activeContext = context.active();
        const resolved = resolveRequestTraceContext({ traceparent: incomingTraceparent });

        expect(resolved).toBe(activeContext);
        expect(trace.getSpanContext(resolved)).toEqual(requestSpan.spanContext());
      });
    } finally {
      requestSpan.end();
    }
  });

  it("uses incoming traceparent and tracestate when no valid span is active", () => {
    context.with(ROOT_CONTEXT, () => {
      const resolved = resolveRequestTraceContext({
        traceparent: incomingTraceparent,
        tracestate: "vendor=value",
      });

      expect(trace.getSpanContext(resolved)).toMatchObject({
        traceId: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        spanId: "bbbbbbbbbbbbbbbb",
        isRemote: true,
      });
      expect(trace.getSpanContext(resolved)?.traceState?.serialize())
        .toBe(createTraceState("vendor=value")?.serialize());
    });
  });

  it("starts a new trace when neither active context nor request headers are valid", () => {
    context.with(ROOT_CONTEXT, () => {
      const resolved = resolveRequestTraceContext({ traceparent: "invalid" });

      expect(trace.getSpanContext(resolved)).toBeUndefined();
      const span = getServerTracer().startSpan("new request work", undefined, resolved);
      try {
        expect(trace.isSpanContextValid(span.spanContext())).toBe(true);
        expect(span.spanContext().traceId).not.toBe("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
      } finally {
        span.end();
      }
    });
  });
});
