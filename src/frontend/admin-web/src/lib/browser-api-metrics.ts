import { metrics, type Counter, type Histogram } from "@opentelemetry/api";

type ApiRequestResult = "success" | "failure";

type BrowserApiRequestMetric = {
  durationSeconds: number;
  method: string;
  operation: string;
  result: ApiRequestResult;
  status: string;
};

let requestDuration: Histogram | undefined;
let requestCount: Counter | undefined;

/**
 * Registers low-cardinality browser API metrics after the global MeterProvider is configured.
 * These metrics represent one actual HTTP attempt; retries are intentionally recorded separately.
 */
export function initializeBrowserApiMetrics() {
  if (requestDuration || requestCount || typeof window === "undefined") {
    return;
  }

  const meter = metrics.getMeter("admin-web.browser");
  requestDuration = meter.createHistogram("frontend.api.request.duration", {
    description: "Duration of a browser API request attempt.",
    unit: "s",
  });
  requestCount = meter.createCounter("frontend.api.request.count", {
    description: "Number of browser API request attempts.",
  });
}

/**
 * Records browser API request metrics without including user input, resource IDs, or raw URLs.
 */
export function recordBrowserApiRequestMetric(metric: BrowserApiRequestMetric) {
  if (!requestDuration || !requestCount || typeof window === "undefined") {
    return;
  }

  const attributes = {
    "http.request.method": metric.method,
    operation: metric.operation,
    result: metric.result,
    status: metric.status,
  };

  requestDuration.record(metric.durationSeconds, attributes);
  requestCount.add(1, attributes);
}
