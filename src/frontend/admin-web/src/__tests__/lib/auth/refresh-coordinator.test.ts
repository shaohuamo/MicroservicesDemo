// @vitest-environment node

import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => {
  const entries = new Map<string, { value: string; expiresAt: number }>();
  const refresh = vi.fn();
  const getRecord = vi.fn();
  const recordExists = vi.fn();
  const updateRecord = vi.fn();
  const tracedCommands: string[] = [];
  const waitSpans: Array<{ name: string; attributes: Record<string, string | number>; ended: boolean }> = [];
  const traceDependency = vi.fn(async (
    _system: string,
    command: string,
    _target: string | undefined,
    operation: () => Promise<unknown>,
  ) => {
    tracedCommands.push(command);
    return operation();
  });
  const startActiveSpan = vi.fn(async (
    name: string,
    _options: unknown,
    callback: (span: {
      setStatus: (status: unknown) => void;
      setAttribute: (key: string, value: string | number) => void;
      end: () => void;
    }) => Promise<unknown>,
  ) => {
    const recorded = { name, attributes: {} as Record<string, string | number>, ended: false };
    waitSpans.push(recorded);
    return callback({
      setStatus: () => undefined,
      setAttribute: (key, value) => { recorded.attributes[key] = value; },
      end: () => { recorded.ended = true; },
    });
  });
  const client = {
    isReady: true,
    on: vi.fn(),
    connect: vi.fn(async () => undefined),
    get: vi.fn(async (key: string) => {
      const entry = entries.get(key);
      if (!entry) return null;
      if (entry.expiresAt <= Date.now()) {
        entries.delete(key);
        return null;
      }
      return entry.value;
    }),
    set: vi.fn(async (key: string, value: string, options: { PX: number; NX: boolean }) => {
      const previous = entries.get(key);
      if (options.NX && previous && previous.expiresAt > Date.now()) return null;
      entries.set(key, { value, expiresAt: Date.now() + options.PX });
      return "OK";
    }),
    eval: vi.fn(async (script: string, options: { keys: string[]; arguments: string[] }) => {
      const [firstKey, secondKey] = options.keys;
      const current = firstKey ? entries.get(firstKey)?.value : undefined;

      if (script.includes("return -1")) {
        if (current !== options.arguments[0]) return 0;
        const previous = secondKey ? entries.get(secondKey)?.value : undefined;
        if (previous && JSON.parse(previous).error === "RefreshTokenMissing") {
          entries.delete(firstKey);
          return -1;
        }
        entries.set(secondKey, {
          value: options.arguments[1],
          expiresAt: Date.now() + Number(options.arguments[2]),
        });
        entries.delete(firstKey);
        return 1;
      }

      if (script.includes("local previous")) {
        entries.set(firstKey, {
          value: options.arguments[0],
          expiresAt: Date.now() + Number(options.arguments[1]),
        });
        if (secondKey) entries.delete(secondKey);
        return current ?? null;
      }

      if (current === options.arguments[0]) {
        entries.delete(firstKey);
        return 1;
      }
      return 0;
    }),
  };
  return { entries, refresh, getRecord, recordExists, updateRecord, tracedCommands, waitSpans, traceDependency, startActiveSpan, client };
});

vi.mock("redis", () => ({ createClient: () => mocks.client }));
vi.mock("@/lib/auth/auth-dependency-tracing", () => ({
  traceAuthDependency: mocks.traceDependency,
}));
vi.mock("@/lib/notifications/server-otel", () => ({
  getAuthTracer: () => ({ startActiveSpan: mocks.startActiveSpan }),
}));
vi.mock("@/lib/auth/identity-server-client", () => ({
  refreshIdentityServerAccessToken: mocks.refresh,
  IdentityServerTokenRefreshError: class IdentityServerTokenRefreshError extends Error {
    constructor(message: string, readonly requiresSignIn: boolean) {
      super(message);
    }
  },
}));
vi.mock("@/lib/auth/refresh-token-store", () => ({
  getRefreshTokenRecord: mocks.getRecord,
  refreshTokenRecordExists: mocks.recordExists,
  updateRefreshTokenRecord: mocks.updateRecord,
  encryptAuthValue: (value: string) => `encrypted:${value}`,
  decryptAuthValue: (value: string) => value.slice("encrypted:".length),
}));

import {
  getOrRefreshAccessToken,
  getRefreshResultTtlSeconds,
  revokeRefreshSession,
} from "@/lib/auth/refresh-coordinator";
import { IdentityServerTokenRefreshError } from "@/lib/auth/identity-server-client";

const resultKey = "admin-web:auth:refresh:result:session-1";

beforeEach(() => {
  mocks.entries.clear();
  mocks.refresh.mockReset();
  mocks.getRecord.mockReset().mockResolvedValue({ id: "session-1", refresh_token: "refresh-token" });
  mocks.recordExists.mockReset().mockResolvedValue(true);
  mocks.updateRecord.mockReset().mockResolvedValue(true);
  mocks.client.connect.mockReset().mockResolvedValue(undefined);
  mocks.client.get.mockClear();
  mocks.client.set.mockClear();
  mocks.client.eval.mockClear();
  mocks.tracedCommands.length = 0;
  mocks.waitSpans.length = 0;
  mocks.traceDependency.mockClear();
  mocks.startActiveSpan.mockClear();
  globalThis.adminWebRefreshRedis = undefined;
});

describe("refresh coordination", () => {
  it("caps success TTL at 30 seconds and ends it before the refresh window", () => {
    const now = 1_000_000;
    expect(getRefreshResultTtlSeconds(now / 1_000 + 900, now)).toBe(30);
    expect(getRefreshResultTtlSeconds(now / 1_000 + 90, now)).toBe(29);
    expect(getRefreshResultTtlSeconds(now / 1_000 + 60, now)).toBe(0);
  });

  it("shares one refresh across concurrent requests and caches encrypted success", async () => {
    let completeRefresh!: (value: object) => void;
    mocks.refresh.mockImplementation(() => new Promise((resolve) => {
      completeRefresh = resolve;
    }));

    const requests = Array.from({ length: 3 }, () => getOrRefreshAccessToken("session-1"));
    await vi.waitFor(() => expect(mocks.refresh).toHaveBeenCalledTimes(1));
    completeRefresh({ access_token: "new-access-token", expires_in: 900, refresh_token: "new-refresh-token" });
    const results = await Promise.all(requests);

    expect(results).toEqual(Array(3).fill(expect.objectContaining({
      status: "success",
      accessToken: "new-access-token",
    })));
    expect(mocks.updateRecord).toHaveBeenCalledTimes(1);
    expect(mocks.entries.get(resultKey)?.value).not.toContain('"accessToken":"new-access-token"');
    expect(mocks.entries.get(resultKey)!.expiresAt - Date.now()).toBeGreaterThan(29_000);
  });

  it("summarizes repeated cache polls in one wait span", async () => {
    mocks.entries.set("admin-web:auth:refresh:lock:session-1", {
      value: "other-owner",
      expiresAt: Date.now() + 50_000,
    });
    const cached = JSON.stringify({
      status: "success",
      payload: 'encrypted:{"accessToken":"shared-token","expiresAt":9999999999}',
    });
    mocks.client.get
      .mockResolvedValueOnce(null)
      .mockResolvedValueOnce(null)
      .mockResolvedValueOnce(null)
      .mockResolvedValueOnce(cached);

    await expect(getOrRefreshAccessToken("session-1")).resolves.toMatchObject({
      status: "success",
      accessToken: "shared-token",
    });

    expect(mocks.tracedCommands.filter((command) => command === "GET")).toHaveLength(1);
    expect(mocks.tracedCommands.filter((command) => command === "SET")).toHaveLength(1);
    expect(mocks.waitSpans).toEqual([expect.objectContaining({
      name: "auth.refresh.wait",
      ended: true,
      attributes: {
        "auth.refresh.poll_count": 3,
        "auth.refresh.lock_attempt_count": 2,
        "auth.refresh.wait.outcome": "success",
      },
    })]);
    expect(mocks.refresh).not.toHaveBeenCalled();
  });

  it("checks the result again after acquiring the lock", async () => {
    const originalSet = mocks.client.set.getMockImplementation()!;
    mocks.client.set.mockImplementationOnce(async (...args) => {
      const acquired = await originalSet(...args);
      mocks.entries.set(resultKey, {
        value: JSON.stringify({
          status: "success",
          payload: 'encrypted:{"accessToken":"other-token","expiresAt":9999999999}',
        }),
        expiresAt: Date.now() + 30_000,
      });
      return acquired;
    });

    await expect(getOrRefreshAccessToken("session-1")).resolves.toMatchObject({
      status: "success",
      accessToken: "other-token",
    });
    expect(mocks.refresh).not.toHaveBeenCalled();
  });

  it("shares failure for 15 seconds instead of retrying for every waiter", async () => {
    mocks.refresh.mockRejectedValue(new Error("IdentityServer unavailable"));
    const results = await Promise.all(
      Array.from({ length: 3 }, () => getOrRefreshAccessToken("session-1")),
    );

    expect(results).toEqual(Array(3).fill({ status: "failure", error: "RefreshUnavailable" }));
    expect(mocks.refresh).toHaveBeenCalledTimes(1);
    expect(mocks.entries.get(resultKey)!.expiresAt - Date.now()).toBeGreaterThan(14_000);
  });

  it("marks an invalid grant as requiring a new sign-in", async () => {
    mocks.refresh.mockRejectedValue(new IdentityServerTokenRefreshError("invalid_grant", true));
    await expect(getOrRefreshAccessToken("session-1")).resolves.toEqual({
      status: "failure",
      error: "RefreshAccessTokenError",
    });
  });

  it("identifies a missing refresh session before calling IdentityServer", async () => {
    mocks.getRecord.mockResolvedValueOnce(null);

    await expect(getOrRefreshAccessToken("session-1")).resolves.toEqual({
      status: "failure",
      error: "RefreshTokenMissing",
    });
    expect(mocks.refresh).not.toHaveBeenCalled();
    expect(mocks.waitSpans.find((span) => span.name === "auth.refresh.attempt")?.attributes)
      .toMatchObject({ "auth.refresh.outcome": "RefreshTokenMissing" });
  });

  it("refreshes directly when the initial Redis read fails", async () => {
    mocks.client.get.mockRejectedValueOnce(new Error("Redis unavailable"));
    mocks.refresh.mockResolvedValue({ access_token: "new-access-token", expires_in: 900 });

    await expect(getOrRefreshAccessToken("session-1")).resolves.toMatchObject({
      status: "success",
      accessToken: "new-access-token",
    });
    expect(mocks.refresh).toHaveBeenCalledTimes(1);
    expect(mocks.recordExists).toHaveBeenCalledWith("session-1");
    expect(mocks.client.set).not.toHaveBeenCalled();
    expect(mocks.entries.has(resultKey)).toBe(false);
  });

  it("retries Redis coordination after an initial connection failure", async () => {
    mocks.client.connect.mockRejectedValueOnce(new Error("Redis unavailable"));
    mocks.refresh.mockResolvedValue({ access_token: "new-access-token", expires_in: 900 });

    const concurrent = await Promise.all(
      Array.from({ length: 3 }, () => getOrRefreshAccessToken("session-1")),
    );
    expect(concurrent).toEqual(Array(3).fill(expect.objectContaining({ status: "success" })));
    expect(mocks.client.connect).toHaveBeenCalledTimes(1);
    expect(mocks.refresh).toHaveBeenCalledTimes(3);
    expect(mocks.client.set).not.toHaveBeenCalled();

    await expect(getOrRefreshAccessToken("session-1")).resolves.toMatchObject({ status: "success" });
    expect(mocks.client.connect).toHaveBeenCalledTimes(2);
    expect(mocks.client.set).toHaveBeenCalledTimes(1);
    expect(mocks.entries.has(resultKey)).toBe(true);
  });

  it("refreshes directly when acquiring the Redis lock fails", async () => {
    mocks.client.set.mockRejectedValueOnce(new Error("Redis unavailable"));
    mocks.refresh.mockResolvedValue({ access_token: "new-access-token", expires_in: 900 });

    await expect(getOrRefreshAccessToken("session-1")).resolves.toMatchObject({ status: "success" });
    expect(mocks.refresh).toHaveBeenCalledTimes(1);
    expect(mocks.entries.has(resultKey)).toBe(false);
  });

  it("refreshes directly when the locked result check fails", async () => {
    mocks.client.get.mockResolvedValueOnce(null).mockRejectedValueOnce(new Error("Redis unavailable"));
    mocks.refresh.mockResolvedValue({ access_token: "new-access-token", expires_in: 900 });

    await expect(getOrRefreshAccessToken("session-1")).resolves.toMatchObject({ status: "success" });
    expect(mocks.refresh).toHaveBeenCalledTimes(1);
    expect(mocks.entries.has(resultKey)).toBe(false);
  });

  it("refreshes directly when Redis fails while waiting for another refresh", async () => {
    mocks.client.set.mockResolvedValueOnce(null);
    mocks.client.get.mockResolvedValueOnce(null).mockRejectedValueOnce(new Error("Redis unavailable"));
    mocks.refresh.mockResolvedValue({ access_token: "new-access-token", expires_in: 900 });

    await expect(getOrRefreshAccessToken("session-1")).resolves.toMatchObject({ status: "success" });
    expect(mocks.refresh).toHaveBeenCalledTimes(1);
    expect(mocks.waitSpans[0]?.attributes["auth.refresh.wait.outcome"]).toBe("error");
  });

  it("refreshes directly when retrying the lock fails during the wait", async () => {
    mocks.client.set.mockResolvedValueOnce(null).mockRejectedValueOnce(new Error("Redis unavailable"));
    mocks.refresh.mockResolvedValue({ access_token: "new-access-token", expires_in: 900 });

    await expect(getOrRefreshAccessToken("session-1")).resolves.toMatchObject({ status: "success" });
    expect(mocks.refresh).toHaveBeenCalledTimes(1);
  });

  it("allows concurrent refreshes while Redis is unavailable", async () => {
    for (let index = 0; index < 3; index++) {
      mocks.client.get.mockRejectedValueOnce(new Error("Redis unavailable"));
    }
    mocks.refresh.mockResolvedValue({ access_token: "new-access-token", expires_in: 900 });

    const results = await Promise.all(
      Array.from({ length: 3 }, () => getOrRefreshAccessToken("session-1")),
    );
    expect(results).toEqual(Array(3).fill(expect.objectContaining({ status: "success" })));
    expect(mocks.refresh).toHaveBeenCalledTimes(3);
    expect(mocks.client.set).not.toHaveBeenCalled();
  });

  it("returns an already refreshed token when publishing to Redis fails", async () => {
    mocks.client.eval.mockRejectedValueOnce(new Error("Redis unavailable"));
    mocks.refresh.mockResolvedValue({
      access_token: "new-access-token",
      expires_in: 900,
      refresh_token: "new-refresh-token",
    });

    await expect(getOrRefreshAccessToken("session-1")).resolves.toMatchObject({
      status: "success",
      accessToken: "new-access-token",
    });
    expect(mocks.refresh).toHaveBeenCalledTimes(1);
    expect(mocks.updateRecord).toHaveBeenCalledWith("session-1", "new-refresh-token");
    expect(mocks.recordExists).toHaveBeenCalledWith("session-1");
    expect(mocks.entries.has(resultKey)).toBe(false);
  });

  it("does not repeat the token exchange if Redis fails while reading a competing result", async () => {
    mocks.client.get.mockResolvedValueOnce(null).mockResolvedValueOnce(null)
      .mockRejectedValueOnce(new Error("Redis unavailable"));
    mocks.client.eval.mockResolvedValueOnce(0);
    mocks.refresh.mockResolvedValue({ access_token: "new-access-token", expires_in: 900 });

    await expect(getOrRefreshAccessToken("session-1")).resolves.toMatchObject({
      status: "success",
      accessToken: "new-access-token",
    });
    expect(mocks.refresh).toHaveBeenCalledTimes(1);
  });

  it("does not return a refreshed token after the session record was deleted", async () => {
    mocks.client.eval.mockRejectedValueOnce(new Error("Redis unavailable"));
    mocks.refresh.mockResolvedValue({
      access_token: "new-access-token",
      expires_in: 900,
      refresh_token: "new-refresh-token",
    });
    mocks.recordExists.mockResolvedValueOnce(false);

    await expect(getOrRefreshAccessToken("session-1")).resolves.toEqual({
      status: "failure",
      error: "RefreshTokenMissing",
    });
    expect(mocks.refresh).toHaveBeenCalledTimes(1);
  });

  it("does not return a token when the final PostgreSQL check fails", async () => {
    mocks.client.get.mockRejectedValueOnce(new Error("Redis unavailable"));
    mocks.refresh.mockResolvedValue({
      access_token: "new-access-token",
      expires_in: 900,
      refresh_token: "new-refresh-token",
    });
    mocks.recordExists.mockRejectedValueOnce(new Error("PostgreSQL unavailable"));

    await expect(getOrRefreshAccessToken("session-1")).resolves.toEqual({
      status: "failure",
      error: "RefreshUnavailable",
    });
    expect(mocks.refresh).toHaveBeenCalledTimes(1);
  });

  it("keeps PostgreSQL and IdentityServer errors distinct from Redis failures", async () => {
    mocks.client.get.mockRejectedValueOnce(new Error("Redis unavailable"));
    mocks.getRecord.mockRejectedValueOnce(new Error("PostgreSQL unavailable"));
    await expect(getOrRefreshAccessToken("session-1")).resolves.toEqual({
      status: "failure",
      error: "RefreshUnavailable",
    });
    expect(mocks.refresh).not.toHaveBeenCalled();

    mocks.client.get.mockRejectedValueOnce(new Error("Redis unavailable"));
    mocks.refresh.mockRejectedValueOnce(new IdentityServerTokenRefreshError("invalid_grant", true));
    await expect(getOrRefreshAccessToken("session-1")).resolves.toEqual({
      status: "failure",
      error: "RefreshAccessTokenError",
    });
  });

  it("does not bypass a malformed Redis result", async () => {
    mocks.entries.set(resultKey, { value: "{invalid", expiresAt: Date.now() + 30_000 });
    await expect(getOrRefreshAccessToken("session-1")).rejects.toThrow(SyntaxError);
    expect(mocks.refresh).not.toHaveBeenCalled();
  });

  it("returns a retryable failure after waiting eight seconds for the lock", async () => {
    const now = vi.spyOn(Date, "now");
    now.mockReturnValueOnce(0).mockReturnValueOnce(8_001);
    mocks.client.set.mockResolvedValueOnce(null);
    try {
      await expect(getOrRefreshAccessToken("session-1")).resolves.toEqual({
        status: "failure",
        error: "RefreshUnavailable",
      });
      expect(mocks.refresh).not.toHaveBeenCalled();
    } finally {
      now.mockRestore();
    }
  });

  it("does not publish a token already inside the refresh window", async () => {
    mocks.refresh.mockResolvedValue({ access_token: "short-lived", expires_in: 60 });
    await expect(getOrRefreshAccessToken("session-1")).resolves.toEqual({
      status: "failure",
      error: "RefreshUnavailable",
    });
    expect(JSON.parse(mocks.entries.get(resultKey)!.value)).toEqual({
      status: "failure",
      error: "RefreshUnavailable",
    });
  });

  it("allows another refresh when the short-lived result is lost", async () => {
    mocks.refresh.mockResolvedValue({ access_token: "new-access-token", expires_in: 900 });
    await getOrRefreshAccessToken("session-1");
    mocks.entries.delete(resultKey);
    await getOrRefreshAccessToken("session-1");
    expect(mocks.refresh).toHaveBeenCalledTimes(2);
  });

  it("prevents an in-flight refresh from publishing after sign-out", async () => {
    let completeRefresh!: (value: object) => void;
    mocks.refresh.mockImplementation(() => new Promise((resolve) => {
      completeRefresh = resolve;
    }));
    const pending = getOrRefreshAccessToken("session-1");
    await vi.waitFor(() => expect(mocks.refresh).toHaveBeenCalledTimes(1));
    await revokeRefreshSession("session-1");
    expect(mocks.entries.has("admin-web:auth:refresh:lock:session-1")).toBe(false);
    completeRefresh({ access_token: "new-access-token", expires_in: 900 });

    await expect(pending).resolves.toEqual({ status: "failure", error: "RefreshTokenMissing" });
    expect(mocks.entries.get(resultKey)?.value).toBe(
      JSON.stringify({ status: "failure", error: "RefreshTokenMissing" }),
    );
  });

  it("removes a cached token on sign-out and returns it for denylisting", async () => {
    mocks.refresh.mockResolvedValue({ access_token: "new-access-token", expires_in: 900 });
    await getOrRefreshAccessToken("session-1");

    await expect(revokeRefreshSession("session-1")).resolves.toBe("new-access-token");
    await expect(getOrRefreshAccessToken("session-1")).resolves.toEqual({
      status: "failure",
      error: "RefreshTokenMissing",
    });
    expect(mocks.refresh).toHaveBeenCalledTimes(1);
  });
});
