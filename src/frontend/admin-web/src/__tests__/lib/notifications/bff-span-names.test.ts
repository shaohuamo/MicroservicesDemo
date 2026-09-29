// @vitest-environment node

import { context, ROOT_CONTEXT, SpanKind, SpanStatusCode, trace, type Span as ApiSpan, type SpanContext } from "@opentelemetry/api";
import { beforeAll, describe, expect, it } from "vitest";
import { initializeServerTelemetry } from "@/lib/notifications/server-otel";

type NamedSpan = ApiSpan & {
  name: string;
  attributes: Record<string, unknown>;
  parentSpanContext?: SpanContext;
  status: { code: SpanStatusCode };
};

describe("BFF proxy framework spans", () => {
  beforeAll(initializeServerTelemetry);

  it("names the routed entry span without changing its parent or error status", () => {
    const parent = trace.getTracer("bff-span-names.test").startSpan("HTTP ingress");
    const entry = trace.getTracer("next.js").startSpan(
      "POST /api/[...path]",
      {
        kind: SpanKind.SERVER,
        attributes: {
          "next.span_type": "BaseServer.handleRequest",
          "next.route": "/api/[...path]",
          "http.method": "POST",
          "http.target": "/api/notifications/01a0e7e4-1ee1-7847-98d1-7537cfa68852/ack?source=browser",
          "http.route": "/api/[...path]",
        },
      },
      trace.setSpan(ROOT_CONTEXT, parent),
    ) as NamedSpan;
    entry.setStatus({ code: SpanStatusCode.ERROR });
    entry.end();

    expect(entry.name).toBe("POST /api/notifications/{notificationId}/ack");
    expect(entry.attributes["http.route"]).toBe("/api/notifications/{notificationId}/ack");
    expect(entry.attributes["next.span_name"]).toBe(entry.name);
    expect(entry.attributes["next.route"]).toBe("/api/[...path]");
    expect(entry.parentSpanContext?.spanId).toBe(parent.spanContext().spanId);
    expect(entry.status.code).toBe(SpanStatusCode.ERROR);
    parent.end();
  });

  it("keeps the handler and gateway spans in the same parent chain", () => {
    const entry = trace.getTracer("next.js").startSpan("GET /api/[...path]", {
      attributes: {
        "next.span_type": "BaseServer.handleRequest",
        "next.route": "/api/[...path]",
        "http.method": "GET",
        "http.target": "/api/products",
      },
    }) as NamedSpan;
    const handler = trace.getTracer("next.js").startSpan(
      "executing api route (app) /api/[...path]",
      {
        attributes: {
          "next.span_type": "AppRouteRouteHandlers.runHandler",
          "next.route": "/api/[...path]",
        },
      },
      trace.setSpan(ROOT_CONTEXT, entry),
    ) as NamedSpan;
    const gateway = context.with(trace.setSpan(ROOT_CONTEXT, handler), () =>
      trace.getTracer("admin-web.notifications").startSpan("gateway GET /gateway/products")
    ) as NamedSpan;

    gateway.end();
    handler.end();
    entry.end();

    expect(entry.name).toBe("GET /api/products");
    expect(handler.name).toBe("BFF proxy request");
    expect(handler.parentSpanContext?.spanId).toBe(entry.spanContext().spanId);
    expect(gateway.parentSpanContext?.spanId).toBe(handler.spanContext().spanId);
    expect(gateway.spanContext().traceId).toBe(entry.spanContext().traceId);
  });

  it("leaves other Next.js routes and the outer ingress span unchanged", () => {
    const outer = trace.getTracer("next.js").startSpan("POST", {
      attributes: { "next.span_type": "BaseServer.handleRequest", "http.method": "POST" },
    }) as NamedSpan;
    const sse = trace.getTracer("next.js").startSpan("GET /api/notifications/stream", {
      attributes: {
        "next.span_type": "BaseServer.handleRequest",
        "next.route": "/api/notifications/stream",
        "http.method": "GET",
        "http.target": "/api/notifications/stream",
      },
    }) as NamedSpan;

    outer.end();
    sse.end();
    expect(outer.name).toBe("POST");
    expect(sse.name).toBe("GET /api/notifications/stream");
  });
});
