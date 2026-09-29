import axios, { AxiosError, type InternalAxiosRequestConfig } from "axios";
import { recordBrowserApiRequestMetric } from "@/lib/browser-api-metrics";

// ---------------------------------------------------------------------------
// Circuit Breaker — opens after consecutive failures, rejects fast until reset
// ---------------------------------------------------------------------------
type CircuitState = "closed" | "open" | "half-open";

export class CircuitBreaker {
  private state: CircuitState = "closed";
  private failureCount = 0;
  private lastFailureTime = 0;

  constructor(
    private readonly failureThreshold = 5,
    private readonly resetTimeoutMs = 30_000,
    private readonly now: () => number = () => Date.now(),
  ) {}

  get isOpen(): boolean {
    if (this.state === "open") {
      if (this.now() - this.lastFailureTime >= this.resetTimeoutMs) {
        this.state = "half-open";
        return false;
      }
      return true;
    }
    return false;
  }

  recordSuccess(): void {
    this.failureCount = 0;
    this.state = "closed";
  }

  recordFailure(): void {
    this.failureCount++;
    this.lastFailureTime = this.now();
    if (this.failureCount >= this.failureThreshold) {
      this.state = "open";
    }
  }
}

// ---------------------------------------------------------------------------
// Retry helpers — exponential back-off with jitter (mirrors Polly defaults)
// ---------------------------------------------------------------------------
const MAX_RETRIES = 3;
const INITIAL_DELAY_MS = 500;
const MAX_DELAY_MS = 5_000;
const REQUEST_TIMEOUT_MS = 10_000;

export function isRetryableError(error: AxiosError): boolean {
  if (error.code === "ERR_CIRCUIT_OPEN") return false;
  if (!error.response) return true; // network error or timeout
  const status = error.response.status;
  return status === 408 || status === 429 || status >= 500;
}

export function getApiErrorMessage(error: AxiosError): string | undefined {
  const data = error.response?.data;
  if (!data || typeof data !== "object") return undefined;

  const detail = (data as { detail?: unknown }).detail;
  return typeof detail === "string" && detail.trim() ? detail : undefined;
}

export function getApiErrorCode(error: unknown): string | undefined {
  if (!axios.isAxiosError(error)) return undefined;

  const data = error.response?.data;
  if (!data || typeof data !== "object") return undefined;

  const errorCode = (data as { errorCode?: unknown }).errorCode;
  return typeof errorCode === "string" && errorCode.trim()
    ? errorCode
    : undefined;
}

export function computeRetryDelay(attempt: number): number {
  const exponential = Math.min(
    INITIAL_DELAY_MS * Math.pow(2, attempt),
    MAX_DELAY_MS,
  );
  return exponential * (0.5 + Math.random() * 0.5);
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// ---------------------------------------------------------------------------
// Resilient Axios instance
// ---------------------------------------------------------------------------
interface RetryableConfig extends InternalAxiosRequestConfig {
  __retryCount?: number;
  __requestStartedAt?: number;
  __requestOperation?: string;
}

const circuitBreaker = new CircuitBreaker();

const api = axios.create({
  baseURL: "/api",
  timeout: REQUEST_TIMEOUT_MS,
});

function getRequestOperation(config: InternalAxiosRequestConfig): string {
  const method = config.method?.toUpperCase() ?? "GET";
  const path = (config.url ?? "").split("?")[0].replace(/\/+$/, "") || "/";

  if (path === "/products") {
    return ({
      GET: "products.list",
      POST: "products.add",
    } as Record<string, string>)[method] ?? "products.other";
  }

  if (/^\/products\/[^/]+$/.test(path)) {
    return ({
      GET: "products.get",
      PUT: "products.update",
      DELETE: "products.delete",
    } as Record<string, string>)[method] ?? "products.other";
  }

  return "other";
}

function getErrorStatus(error: AxiosError): string {
  if (error.code === "ERR_CIRCUIT_OPEN") return "circuit_open";
  if (error.code === "ECONNABORTED") return "timeout";
  if (!error.response) return "network_error";
  return `${Math.floor(error.response.status / 100)}xx`;
}

function recordRequestMetric(
  config: RetryableConfig | undefined,
  result: "success" | "failure",
  status: string,
) {
  if (!config) return;

  const elapsedMilliseconds = Math.max(
    0,
    performance.now() - (config.__requestStartedAt ?? performance.now()),
  );
  recordBrowserApiRequestMetric({
    durationSeconds: elapsedMilliseconds / 1_000,
    method: config.method?.toUpperCase() ?? "GET",
    operation: config.__requestOperation ?? getRequestOperation(config),
    result,
    status,
  });
}

// Request interceptor: reject immediately when circuit is open
api.interceptors.request.use((config) => {
  if (circuitBreaker.isOpen) {
    return Promise.reject(
      new AxiosError(
        "Circuit breaker is open — service appears unavailable. Please try again later.",
        "ERR_CIRCUIT_OPEN",
        config,
      ),
    );
  }

  const retryableConfig = config as RetryableConfig;
  retryableConfig.__requestStartedAt = performance.now();
  retryableConfig.__requestOperation = getRequestOperation(config);
  return config;
});

// Response interceptor: retry transient failures with exponential back-off + jitter
api.interceptors.response.use(
  (response) => {
    circuitBreaker.recordSuccess();
    recordRequestMetric(
      response.config as RetryableConfig,
      "success",
      `${Math.floor(response.status / 100)}xx`,
    );
    return response;
  },
  async (error: AxiosError) => {
    const config = error.config as RetryableConfig | undefined;
    if (!config) return Promise.reject(error);

    recordRequestMetric(config, "failure", getErrorStatus(error));

    config.__retryCount ??= 0;

    if (config.__retryCount < MAX_RETRIES && isRetryableError(error)) {
      config.__retryCount++;
      await delay(computeRetryDelay(config.__retryCount - 1));
      return api.request(config);
    }

    circuitBreaker.recordFailure();
    return Promise.reject(error);
  },
);

export { api };
