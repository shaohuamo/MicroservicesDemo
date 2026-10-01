import { afterEach, describe, it, expect, vi } from "vitest";
import { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from "axios";
import {
  CircuitBreaker,
  api,
  getApiErrorMessage,
  isRetryableError,
  computeRetryDelay,
} from "@/lib/api/http-client";
import { getRequestHeadersForLog } from "@/lib/dev-http-logging";

function makeAxiosError(
  status?: number,
  code?: string,
  data: unknown = null,
): AxiosError {
  const headers = new AxiosHeaders();
  const config = { headers } as InternalAxiosRequestConfig;
  const response = status
    ? ({
        status,
        data,
        statusText: "",
        headers,
        config,
      } as unknown as import("axios").AxiosResponse)
    : undefined;
  return new AxiosError("test", code, config, null, response);
}

// ---------------------------------------------------------------------------
// CircuitBreaker
// ---------------------------------------------------------------------------
describe("CircuitBreaker", () => {
  it("starts in closed state", () => {
    const cb = new CircuitBreaker();
    expect(cb.isOpen).toBe(false);
  });

  it("opens after reaching the failure threshold", () => {
    const cb = new CircuitBreaker(3, 30_000);
    cb.recordFailure();
    cb.recordFailure();
    expect(cb.isOpen).toBe(false);
    cb.recordFailure();
    expect(cb.isOpen).toBe(true);
  });

  it("resets failure count on success", () => {
    const cb = new CircuitBreaker(3, 30_000);
    cb.recordFailure();
    cb.recordFailure();
    cb.recordSuccess();
    cb.recordFailure();
    expect(cb.isOpen).toBe(false);
  });

  it("transitions to half-open after reset timeout", () => {
    let time = 0;
    const cb = new CircuitBreaker(2, 1_000, () => time);
    cb.recordFailure();
    cb.recordFailure();
    expect(cb.isOpen).toBe(true);

    time = 1_000;
    expect(cb.isOpen).toBe(false); // half-open — allows a probe request
  });

  it("closes again after success in half-open state", () => {
    let time = 0;
    const cb = new CircuitBreaker(2, 1_000, () => time);
    cb.recordFailure();
    cb.recordFailure();

    time = 1_000;
    cb.isOpen; // triggers transition to half-open
    cb.recordSuccess(); // probe succeeded

    cb.recordFailure(); // single failure after reset
    expect(cb.isOpen).toBe(false); // still closed — threshold not reached
  });

  it("re-opens from half-open after another failure burst", () => {
    let time = 0;
    const cb = new CircuitBreaker(2, 1_000, () => time);
    cb.recordFailure();
    cb.recordFailure();
    expect(cb.isOpen).toBe(true);

    time = 1_000;
    cb.isOpen; // half-open
    cb.recordFailure();
    cb.recordFailure();
    expect(cb.isOpen).toBe(true); // open again
  });
});

// ---------------------------------------------------------------------------
// isRetryableError
// ---------------------------------------------------------------------------
describe("isRetryableError", () => {
  it("returns true for network errors (no response)", () => {
    expect(isRetryableError(makeAxiosError())).toBe(true);
  });

  it.each([408, 429, 500, 502, 503, 504])(
    "returns true for status %i",
    (status) => {
      expect(isRetryableError(makeAxiosError(status))).toBe(true);
    },
  );

  it.each([400, 401, 403, 404, 409, 422])(
    "returns false for status %i",
    (status) => {
      expect(isRetryableError(makeAxiosError(status))).toBe(false);
    },
  );

  it("returns false for circuit-breaker-open errors", () => {
    expect(
      isRetryableError(makeAxiosError(undefined, "ERR_CIRCUIT_OPEN")),
    ).toBe(false);
  });
});

describe("expired browser session", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("starts a new sign-in after the BFF returns 401", async () => {
    const replace = vi.fn();
    vi.stubGlobal("window", { location: { replace } });

    await expect(api.get("/products", {
      adapter: async (config) => {
        throw new AxiosError("Unauthorized", undefined, config, null, {
          status: 401,
          statusText: "Unauthorized",
          data: null,
          headers: new AxiosHeaders(),
          config,
        });
      },
    })).rejects.toMatchObject({ response: { status: 401 } });

    expect(replace).toHaveBeenCalledOnce();
    expect(replace).toHaveBeenCalledWith("/logout");
  });
});

describe("getApiErrorMessage", () => {
  it("returns the detail from a ProblemDetails response", () => {
    const error = makeAxiosError(409, undefined, {
      code: "product.concurrency_conflict",
      detail: "Refresh the product and try again.",
    });

    expect(getApiErrorMessage(error)).toBe("Refresh the product and try again.");
  });

  it("returns undefined for a response without detail", () => {
    expect(getApiErrorMessage(makeAxiosError(500))).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// computeRetryDelay
// ---------------------------------------------------------------------------
describe("computeRetryDelay", () => {
  it("returns a delay within expected range for attempt 0", () => {
    const d = computeRetryDelay(0);
    // base = 500, jitter range [0.5, 1.0) → [250, 500]
    expect(d).toBeGreaterThanOrEqual(250);
    expect(d).toBeLessThanOrEqual(500);
  });

  it("increases delay for later attempts", () => {
    const d = computeRetryDelay(2);
    // base = min(500*4, 5000) = 2000, range [1000, 2000]
    expect(d).toBeGreaterThanOrEqual(1000);
    expect(d).toBeLessThanOrEqual(2000);
  });

  it("caps delay at maximum", () => {
    const d = computeRetryDelay(10);
    expect(d).toBeLessThanOrEqual(5000);
  });
});

describe("development HTTP logging", () => {
  it("redacts the idempotency key", () => {
    const headers = new Headers({
      "Idempotency-Key": "7f277273-b334-47f9-8b59-d37aa3473665",
      "Content-Type": "application/json",
    });

    expect(getRequestHeadersForLog(headers)["idempotency-key"]).toBe("[REDACTED]");
  });

  it("redacts credentials and omits internal proof headers", () => {
    const headers = new Headers({
      authorization: "Bearer access-token",
      cookie: "authjs.session-token=encrypted",
      "x-admin-session-id": "11111111-1111-1111-1111-111111111111",
      "x-admin-proof-iat": "1234567890",
      "x-admin-proof-kid": "v1",
      "x-admin-proof-sig": "signature",
    });
    const logged = getRequestHeadersForLog(headers, [
      "x-admin-session-id", "x-admin-proof-iat", "x-admin-proof-kid", "x-admin-proof-sig",
    ]);
    expect(logged.authorization).toBe("[REDACTED]");
    expect(logged.cookie).toBe("[REDACTED]");
    expect(logged["x-admin-session-id"]).toBeUndefined();
    expect(logged["x-admin-proof-iat"]).toBeUndefined();
    expect(logged["x-admin-proof-kid"]).toBeUndefined();
    expect(logged["x-admin-proof-sig"]).toBeUndefined();
  });
});
