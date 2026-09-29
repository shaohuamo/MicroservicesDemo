import { metrics } from "@opentelemetry/api";
import { OTLPMetricExporter } from "@opentelemetry/exporter-metrics-otlp-http";
import { BatchSpanProcessor } from "@opentelemetry/sdk-trace-base";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-http";
import { resourceFromAttributes } from "@opentelemetry/resources";
import { W3CTraceContextPropagator } from "@opentelemetry/core";
import { registerInstrumentations } from "@opentelemetry/instrumentation";
import { getWebAutoInstrumentations } from "@opentelemetry/auto-instrumentations-web";
import {
  MeterProvider,
  PeriodicExportingMetricReader,
} from "@opentelemetry/sdk-metrics";
import { WebTracerProvider } from "@opentelemetry/sdk-trace-web";
import { initializeBrowserApiMetrics } from "@/lib/browser-api-metrics";

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
  const metricExporter = new OTLPMetricExporter({
    url: "/otel/v1/metrics",
  });

  const provider = new WebTracerProvider({
    resource,
    spanProcessors: [new BatchSpanProcessor(exporter)],
  });
  provider.register({
    propagator: new W3CTraceContextPropagator(),
  });

  const meterProvider = new MeterProvider({
    resource,
    readers: [
      new PeriodicExportingMetricReader({
        exporter: metricExporter,
        exportIntervalMillis: 60_000,
      }),
    ],
  });
  metrics.setGlobalMeterProvider(meterProvider);
  initializeBrowserApiMetrics();

  window.addEventListener("pagehide", () => {
    void meterProvider.forceFlush();
  });

  registerInstrumentations({
    instrumentations: [
      getWebAutoInstrumentations({
        "@opentelemetry/instrumentation-fetch": {
          ignoreUrls: [/\/otel\//, /\/_next\//, /[?&]_rsc=/],
        },
        "@opentelemetry/instrumentation-xml-http-request": {
          ignoreUrls: [/\/otel\//, /\/_next\//, /[?&]_rsc=/],
        },
        "@opentelemetry/instrumentation-user-interaction": {
          enabled: false,
        },
      }),
    ],
  });
}
