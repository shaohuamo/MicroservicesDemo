import axios, { AxiosError, type InternalAxiosRequestConfig } from "axios";

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
}

const circuitBreaker = new CircuitBreaker();

const api = axios.create({
  baseURL: "/api",
  timeout: REQUEST_TIMEOUT_MS,
});

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
  return config;
});

// Response interceptor: retry transient failures with exponential back-off + jitter
api.interceptors.response.use(
  (response) => {
    circuitBreaker.recordSuccess();
    return response;
  },
  async (error: AxiosError) => {
    const config = error.config as RetryableConfig | undefined;
    if (!config) return Promise.reject(error);

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
