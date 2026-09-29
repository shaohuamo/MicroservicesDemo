import {
  context,
  isSpanContextValid,
  propagation,
  ROOT_CONTEXT,
  trace,
  type Context,
  type Span as ApiSpan,
} from "@opentelemetry/api";
import { logs, SeverityNumber, type Logger } from "@opentelemetry/api-logs";
import { OTLPLogExporter } from "@opentelemetry/exporter-logs-otlp-http";
import { W3CTraceContextPropagator } from "@opentelemetry/core";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-http";
import { resourceFromAttributes } from "@opentelemetry/resources";
import { BatchLogRecordProcessor, LoggerProvider } from "@opentelemetry/sdk-logs";
import {
  BatchSpanProcessor,
  type ReadableSpan,
  type Span as SdkSpan,
  type SpanProcessor,
} from "@opentelemetry/sdk-trace-base";
import { NodeTracerProvider } from "@opentelemetry/sdk-trace-node";
import { resolveBffProxyRoute } from "@/lib/api/bff-route-template";

let initialized = false;
let serverLogger: Logger | undefined;

export type TraceContextCarrier = {
  traceparent?: string;
  tracestate?: string;
};

function isServerNoiseSpan(spanName: string) {
  const normalizedName = spanName.toLowerCase();
  const identityServerInternalUrl = (process.env.IDENTITYSERVER_INTERNAL_URL ?? "").toLowerCase();
  const isDuplicateIdentityServerFetch =
    normalizedName.startsWith("fetch ")
    && identityServerInternalUrl.length > 0
    && normalizedName.includes(identityServerInternalUrl);

  return (
    spanName.startsWith("RSC ")
    || normalizedName.startsWith("resolve page components")
    || normalizedName.startsWith("middleware ")
    || normalizedName.startsWith("start response")
    || normalizedName.startsWith("generatemetadata ")
    || normalizedName.includes("/otel")
    || normalizedName.includes("/_next/")
    || isDuplicateIdentityServerFetch
  );
}

function isProductsPageRequest(span: SdkSpan | ReadableSpan) {
  return span.instrumentationScope.name === "next.js"
    && span.attributes["next.span_type"] === "BaseServer.handleRequest"
    && (span.attributes["http.target"] as string | undefined)?.split("?")[0] === "/products";
}

const PRODUCTS_PAGE_FRAMEWORK_SPANS = new Set([
  "BaseServer.handleRequest",
  "AppRender.getBodyResult",
  "NextNodeServer.createComponentTree",
  "NextNodeServer.getLayoutOrPageModule",
  "NextNodeServer.clientComponentLoading",
]);

function nameBffProxySpan(span: SdkSpan) {
  if (span.instrumentationScope.name !== "next.js"
    || span.attributes["next.route"] !== "/api/[...path]") return;

  const spanType = span.attributes["next.span_type"];
  if (spanType === "AppRouteRouteHandlers.runHandler") {
    span.updateName("BFF proxy request");
    span.setAttribute("next.span_name", "BFF proxy request");
    return;
  }

  if (spanType !== "BaseServer.handleRequest") return;
  const method = span.attributes["http.method"];
  const target = span.attributes["http.target"];
  if (typeof method !== "string" || typeof target !== "string") return;

  const route = resolveBffProxyRoute(method, target);
  const name = `${method.toUpperCase()} ${route}`;
  span.updateName(name);
  span.setAttribute("http.route", route);
  span.setAttribute("next.span_name", name);
}

class FilteringSpanProcessor implements SpanProcessor {
  private readonly productsPageTraceIds = new Set<string>();

  constructor(private readonly delegate: SpanProcessor) {}

  onStart(span: SdkSpan, parentContext: Context) {
    if (isProductsPageRequest(span)) {
      this.productsPageTraceIds.add(span.spanContext().traceId);
    }

    if (!this.isNoiseSpan(span)) {
      this.delegate.onStart(span, parentContext);
    }
  }

  onEnd(span: ReadableSpan) {
    if (!this.isNoiseSpan(span)) {
      this.delegate.onEnd(span);
    }

    if (isProductsPageRequest(span)) {
      this.productsPageTraceIds.delete(span.spanContext().traceId);
    }
  }

  onEnding(span: SdkSpan) {
    nameBffProxySpan(span);
    if (!this.isNoiseSpan(span)) {
      this.delegate.onEnding?.(span);
    }
  }

  private isNoiseSpan(span: SdkSpan | ReadableSpan) {
    return isServerNoiseSpan(span.name)
      || (span.instrumentationScope.name === "next.js"
        && this.productsPageTraceIds.has(span.spanContext().traceId)
        && PRODUCTS_PAGE_FRAMEWORK_SPANS.has(String(span.attributes["next.span_type"])));
  }

  forceFlush() {
    return this.delegate.forceFlush();
  }

  shutdown() {
    return this.delegate.shutdown();
  }
}

function getCollectorUrl(signal: "traces" | "logs") {
  const baseUrl = (process.env.OTEL_COLLECTOR_INTERNAL_URL || "http://localhost:4318")
    .replace(/\/$/, "");
  return `${baseUrl}/v1/${signal}`;
}

export function initializeServerTelemetry() {
  if (initialized || typeof window !== "undefined") return;
  initialized = true;

  const provider = new NodeTracerProvider({
    resource: resourceFromAttributes({
      "service.name": "admin-web",
    }),
    spanProcessors: [
      new FilteringSpanProcessor(
        new BatchSpanProcessor(new OTLPTraceExporter({ url: getCollectorUrl("traces") })),
      ),
    ],
  });

  provider.register({ propagator: new W3CTraceContextPropagator() });

  const loggerProvider = new LoggerProvider({
    resource: resourceFromAttributes({
      "service.name": "admin-web",
    }),
    processors: [
      new BatchLogRecordProcessor(new OTLPLogExporter({ url: getCollectorUrl("logs") })),
    ],
  });
  logs.setGlobalLoggerProvider(loggerProvider);
  serverLogger = loggerProvider.getLogger("admin-web.business");
}

export function getServerTracer() {
  initializeServerTelemetry();
  return trace.getTracer("admin-web.notifications");
}

export function getAuthTracer() {
  initializeServerTelemetry();
  return trace.getTracer("admin-web.auth");
}

export function logServerEvent(
  severityNumber: SeverityNumber,
  severityText: "INFO" | "WARN" | "ERROR",
  body: string,
  attributes: Record<string, string | number | boolean> = {},
) {
  initializeServerTelemetry();
  serverLogger?.emit({
    severityNumber,
    severityText,
    body,
    attributes,
    context: context.active(),
  });
}

export { SeverityNumber };

export function extractTraceContext(carrier: TraceContextCarrier): Context {
  initializeServerTelemetry();
  return propagation.extract(ROOT_CONTEXT, carrier, {
    keys: (value) => Object.keys(value),
    get: (value, key) => value[key as keyof TraceContextCarrier],
  });
}

export function resolveRequestTraceContext(carrier: TraceContextCarrier): Context {
  initializeServerTelemetry();
  const activeContext = context.active();
  const activeSpanContext = trace.getSpanContext(activeContext);

  if (activeSpanContext && isSpanContextValid(activeSpanContext)) {
    return activeContext;
  }

  return extractTraceContext(carrier);
}

export function injectSpanContext(span: ApiSpan): TraceContextCarrier {
  initializeServerTelemetry();
  const carrier: TraceContextCarrier = {};
  propagation.inject(trace.setSpan(ROOT_CONTEXT, span), carrier, {
    set: (value, key, content) => {
      value[key as keyof TraceContextCarrier] = content;
    },
  });
  return carrier;
}
