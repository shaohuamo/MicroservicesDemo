import { WebTracerProvider } from "@opentelemetry/sdk-trace-web";
import { BatchSpanProcessor } from "@opentelemetry/sdk-trace-base";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-http";
import { resourceFromAttributes } from "@opentelemetry/resources";
import { W3CTraceContextPropagator } from "@opentelemetry/core";
import { registerInstrumentations } from "@opentelemetry/instrumentation";
import { getWebAutoInstrumentations } from "@opentelemetry/auto-instrumentations-web";

let initialized = false;

export function initOpenTelemetry() {
  if (initialized || typeof window === "undefined") return;
  initialized = true;

  const resource = resourceFromAttributes({
    "service.name": "admin-web",
  });

  const exporter = new OTLPTraceExporter({
    url: "/otel/v1/traces",
  });

  const provider = new WebTracerProvider({
    resource,
    spanProcessors: [new BatchSpanProcessor(exporter)],
  });
  provider.register({
    propagator: new W3CTraceContextPropagator(),
  });

  registerInstrumentations({
    instrumentations: [
      getWebAutoInstrumentations({
        "@opentelemetry/instrumentation-fetch": {
          ignoreUrls: [/\/otel\//, /\/_next\//],
        },
        "@opentelemetry/instrumentation-xml-http-request": {
          ignoreUrls: [/\/otel\//, /\/_next\//],
        },
        "@opentelemetry/instrumentation-user-interaction": {
          eventNames: ["click"],
        },
      }),
    ],
  });
}
