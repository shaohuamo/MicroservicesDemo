import { randomUUID } from "crypto";
import { createClient } from "redis";
import { SpanKind, SpanStatusCode } from "@opentelemetry/api";
import { traceAuthDependency } from "@/lib/auth/auth-dependency-tracing";
import { getAuthTracer } from "@/lib/notifications/server-otel";
import {
  IdentityServerTokenRefreshError,
  refreshIdentityServerAccessToken,
} from "@/lib/auth/identity-server-client";
import {
  decryptAuthValue,
  encryptAuthValue,
  getRefreshTokenRecord,
  refreshTokenRecordExists,
  updateRefreshTokenRecord,
} from "@/lib/auth/refresh-token-store";

function createRefreshRedisClient() {
  return createClient({
    url: process.env.AUTH_REDIS_URL || "redis://localhost:6379",
    disableOfflineQueue: true,
    socket: { connectTimeout: 1_000, reconnectStrategy: false },
  });
}

type RedisClient = ReturnType<typeof createRefreshRedisClient>;
type RefreshError = "RefreshTokenMissing" | "RefreshAccessTokenError" | "RefreshUnavailable";
type RefreshSuccess = {
  status: "success";
  accessToken: string;
  idToken?: string;
  expiresAt: number;
};
type RefreshFailure = { status: "failure"; error: RefreshError };
export type RefreshResult = RefreshSuccess | RefreshFailure;

declare global {
  var adminWebRefreshRedis: Promise<RedisClient> | undefined;
}

const RESULT_PREFIX = "admin-web:auth:refresh:result:";
const LOCK_PREFIX = "admin-web:auth:refresh:lock:";
const REFRESH_WINDOW_SECONDS = 60;
const SUCCESS_MAX_TTL_SECONDS = 30;
const FAILURE_TTL_MS = 15_000;
const SIGNED_OUT_TTL_MS = 60_000;
const LOCK_TTL_MS = 50_000;
const REFRESH_TIMEOUT_MS = 45_000;
const WAITER_TIMEOUT_MS = 8_000;

class RedisRefreshCoordinationError extends Error {
  constructor(readonly stage: string) {
    super("Redis refresh coordination is unavailable.");
  }
}

async function redisOperation<T>(stage: string, operation: () => Promise<T>): Promise<T> {
  try {
    return await operation();
  } catch {
    throw new RedisRefreshCoordinationError(stage);
  }
}

const PUBLISH_SCRIPT = `
if redis.call('GET', KEYS[1]) ~= ARGV[1] then return 0 end
local previous = redis.call('GET', KEYS[2])
if previous then
  local valid, value = pcall(cjson.decode, previous)
  if valid and value.status == 'failure' and value.error == 'RefreshTokenMissing' then
    redis.call('DEL', KEYS[1])
    return -1
  end
end
redis.call('PSETEX', KEYS[2], tonumber(ARGV[3]), ARGV[2])
redis.call('DEL', KEYS[1])
return 1
`;

const RELEASE_SCRIPT = `
if redis.call('GET', KEYS[1]) == ARGV[1] then
  return redis.call('DEL', KEYS[1])
end
return 0
`;

const SIGN_OUT_SCRIPT = `
local previous = redis.call('GET', KEYS[1])
redis.call('PSETEX', KEYS[1], tonumber(ARGV[2]), ARGV[1])
redis.call('DEL', KEYS[2])
return previous
`;

function resultKey(recordId: string) {
  return `${RESULT_PREFIX}${recordId}`;
}

function lockKey(recordId: string) {
  return `${LOCK_PREFIX}${recordId}`;
}

async function getRedis(): Promise<RedisClient> {
  const existing = globalThis.adminWebRefreshRedis;
  if (existing) {
    try {
      const client = await existing;
      if (client.isReady) return client;
    } catch (error) {
      if (globalThis.adminWebRefreshRedis === existing) {
        globalThis.adminWebRefreshRedis = undefined;
      }
      throw error;
    }
    if (globalThis.adminWebRefreshRedis !== existing) return getRedis();
    globalThis.adminWebRefreshRedis = undefined;
  }

  const connecting = (async () => {
    const client = createRefreshRedisClient();
    client.on("error", () => {
      // Command failures are reported to the caller.
    });
    await traceAuthDependency("redis", "CONNECT", undefined, () =>
      redisOperation("connect", () => client.connect()));
    return client;
  })();
  globalThis.adminWebRefreshRedis = connecting;

  try {
    return await connecting;
  } catch (error) {
    if (globalThis.adminWebRefreshRedis === connecting) {
      globalThis.adminWebRefreshRedis = undefined;
    }
    throw error;
  }
}

export function getRefreshResultTtlSeconds(expiresAt: number, nowMs = Date.now()) {
  const secondsUntilRefreshWindow = Math.floor(
    expiresAt - nowMs / 1_000 - REFRESH_WINDOW_SECONDS - 1,
  );
  return Math.max(0, Math.min(SUCCESS_MAX_TTL_SECONDS, secondsUntilRefreshWindow));
}

function failure(error: RefreshError): RefreshFailure {
  return { status: "failure", error };
}

function decodeResult(raw: string | null): RefreshResult | null {
  if (raw === null) return null;

  const result: unknown = JSON.parse(raw);
  if (!result || typeof result !== "object" || !("status" in result)) {
    throw new Error("Invalid refresh result in Redis.");
  }

  if (result.status === "failure" && "error" in result) {
    if (result.error === "RefreshTokenMissing"
      || result.error === "RefreshAccessTokenError"
      || result.error === "RefreshUnavailable") {
      return failure(result.error);
    }
  }

  if (result.status === "success" && "payload" in result && typeof result.payload === "string") {
    const payload: unknown = JSON.parse(decryptAuthValue(result.payload));
    if (payload && typeof payload === "object"
      && "accessToken" in payload && typeof payload.accessToken === "string"
      && "expiresAt" in payload && typeof payload.expiresAt === "number") {
      return {
        status: "success",
        accessToken: payload.accessToken,
        expiresAt: payload.expiresAt,
        idToken: "idToken" in payload && typeof payload.idToken === "string"
          ? payload.idToken
          : undefined,
      };
    }
  }

  throw new Error("Invalid refresh result in Redis.");
}

async function readResult(
  redis: RedisClient,
  recordId: string,
  traced = true,
  stage = "result_read",
) {
  const get = () => redisOperation(stage, () => redis.get(resultKey(recordId)));
  const raw = traced
    ? await traceAuthDependency("redis", "GET", "refresh.result", get)
    : await get();
  return decodeResult(raw);
}

async function tryAcquireLock(redis: RedisClient, recordId: string, owner: string, traced = true) {
  const set = () => redisOperation(traced ? "lock_acquire" : "lock_wait", () =>
    redis.set(lockKey(recordId), owner, {
      NX: true,
      PX: LOCK_TTL_MS,
    }));
  return traced
    ? traceAuthDependency("redis", "SET", "refresh.lock", set)
    : set();
}

async function releaseLock(redis: RedisClient, recordId: string, owner: string) {
  await traceAuthDependency("redis", "EVAL", "refresh.release", () => redis.eval(RELEASE_SCRIPT, {
    keys: [lockKey(recordId)],
    arguments: [owner],
  }));
}

async function publishResult(
  redis: RedisClient,
  recordId: string,
  owner: string,
  result: RefreshResult,
  ttlMs: number,
) {
  const value = result.status === "success"
    ? JSON.stringify({
        status: "success",
        payload: encryptAuthValue(JSON.stringify({
          accessToken: result.accessToken,
          idToken: result.idToken,
          expiresAt: result.expiresAt,
        })),
      })
    : JSON.stringify(result);
  const published = await traceAuthDependency("redis", "EVAL", "refresh.publish", () =>
    redisOperation("result_publish", () => redis.eval(PUBLISH_SCRIPT, {
      keys: [lockKey(recordId), resultKey(recordId)],
      arguments: [owner, value, String(ttlMs)],
    })));
  return Number(published);
}

async function refreshOnce(recordId: string): Promise<{ result: RefreshResult; ttlMs: number }> {
  return getAuthTracer().startActiveSpan(
    "auth.refresh.attempt",
    { kind: SpanKind.INTERNAL },
    async (span) => {
      let result: RefreshResult;
      let ttlMs = FAILURE_TTL_MS;
      try {
        const record = await getRefreshTokenRecord(recordId);
        if (!record) {
          result = failure("RefreshTokenMissing");
        } else {
          const refreshed = await refreshIdentityServerAccessToken(
            record.refresh_token,
            AbortSignal.timeout(REFRESH_TIMEOUT_MS),
          );
          if (!refreshed.access_token || !Number.isFinite(refreshed.expires_in)
            || !refreshed.expires_in || refreshed.expires_in <= 0) {
            throw new Error("Token refresh did not return a usable access token and lifetime.");
          }

          const expiresAt = Math.floor(Date.now() / 1_000) + refreshed.expires_in;
          const ttlSeconds = getRefreshResultTtlSeconds(expiresAt);
          if (ttlSeconds <= 0) {
            throw new Error("Refreshed access token is already inside the refresh window.");
          }

          if (refreshed.refresh_token) {
            if (!await updateRefreshTokenRecord(recordId, refreshed.refresh_token)) {
              result = failure("RefreshTokenMissing");
            } else {
              result = {
                status: "success",
                accessToken: refreshed.access_token,
                idToken: refreshed.id_token,
                expiresAt,
              };
            }
          } else if (!await refreshTokenRecordExists(recordId)) {
            result = failure("RefreshTokenMissing");
          } else {
            result = {
              status: "success",
              accessToken: refreshed.access_token,
              idToken: refreshed.id_token,
              expiresAt,
            };
          }

          if (result.status === "success") {
            ttlMs = getRefreshResultTtlSeconds(expiresAt) * 1_000;
            if (ttlMs <= 0) {
              result = failure("RefreshAccessTokenError");
              ttlMs = FAILURE_TTL_MS;
            }
          }
        }
      } catch (error) {
        result = failure(
          error instanceof IdentityServerTokenRefreshError && error.requiresSignIn
            ? "RefreshAccessTokenError"
            : "RefreshUnavailable",
        );
      }

      span.setAttribute("auth.refresh.outcome",
        result.status === "success" ? "success" : result.error);
      span.setStatus({ code: result.status === "success" ? SpanStatusCode.OK : SpanStatusCode.ERROR });
      span.end();
      return { result, ttlMs };
    },
  );
}

async function refreshWithoutRedis(
  recordId: string,
  stage: string,
  completed?: RefreshResult,
): Promise<RefreshResult> {
  return getAuthTracer().startActiveSpan(
    "auth.refresh.redis_fallback",
    { kind: SpanKind.INTERNAL, attributes: { "auth.refresh.redis_failure_stage": stage } },
    async (span) => {
      try {
        let result = completed ?? (await refreshOnce(recordId)).result;
        if (result.status === "success") {
          try {
            if (!await refreshTokenRecordExists(recordId)) {
              result = failure("RefreshTokenMissing");
            }
          } catch {
            result = failure("RefreshUnavailable");
          }
        }
        span.setAttribute("auth.refresh.fallback.outcome",
          result.status === "success" ? "success" : result.error);
        return result;
      } finally {
        span.end();
      }
    },
  );
}

async function refreshUnderLock(redis: RedisClient, recordId: string, owner: string): Promise<RefreshResult> {
  try {
    // Another request may have published the result between our first read and SET NX.
    const existing = await readResult(redis, recordId, true, "locked_result_read");
    if (existing) return existing;

    const { result, ttlMs } = await refreshOnce(recordId);

    let published: number;
    try {
      published = await publishResult(redis, recordId, owner, result, ttlMs);
    } catch (error) {
      if (error instanceof RedisRefreshCoordinationError) {
        // The token exchange may already have succeeded. Never repeat it.
        return refreshWithoutRedis(recordId, error.stage, result);
      }
      throw error;
    }

    if (published === -1) return failure("RefreshTokenMissing");
    if (published !== 1) {
      // Sign-out revokes the lease, while an expired lease may have been taken
      // over by another request. Return the state that actually won.
      try {
        return await readResult(redis, recordId, true, "winner_result_read")
          ?? failure("RefreshUnavailable");
      } catch (error) {
        if (error instanceof RedisRefreshCoordinationError) {
          return refreshWithoutRedis(recordId, error.stage, result);
        }
        throw error;
      }
    }
    return result;
  } finally {
    // This is a no-op if publishResult already released our lock. Redis errors
    // here must not hide a result that was successfully published.
    try {
      await releaseLock(redis, recordId, owner);
    } catch {
      // The lease expires even if Redis becomes unavailable during cleanup.
    }
  }
}

async function waitForRefreshResult(redis: RedisClient, recordId: string, deadline: number) {
  return getAuthTracer().startActiveSpan(
    "auth.refresh.wait",
    { kind: SpanKind.INTERNAL },
    async (span): Promise<{ result: RefreshResult } | { owner: string }> => {
      let polls = 0;
      let lockAttempts = 0;
      let outcome = "error";
      try {
        for (;;) {
          const remainingMs = deadline - Date.now();
          if (remainingMs <= 0) {
            outcome = "timeout";
            span.setStatus({ code: SpanStatusCode.ERROR });
            return { result: failure("RefreshUnavailable") };
          }

          await new Promise((resolve) => setTimeout(resolve, Math.min(200, remainingMs)));
          // Repeated polls are represented by this one span and its counters.
          const existing = await readResult(redis, recordId, false, "wait_result_read");
          polls++;
          if (existing) {
            outcome = existing.status === "success" ? "success" : "failure";
            return { result: existing };
          }

          const owner = randomUUID();
          const acquired = await tryAcquireLock(redis, recordId, owner, false);
          lockAttempts++;
          if (acquired === "OK") {
            outcome = "lock_acquired";
            return { owner };
          }
        }
      } catch (error) {
        span.setStatus({ code: SpanStatusCode.ERROR });
        span.setAttribute("error.type", error instanceof Error ? error.name : "UnknownError");
        throw error;
      } finally {
        span.setAttribute("auth.refresh.poll_count", polls);
        span.setAttribute("auth.refresh.lock_attempt_count", lockAttempts);
        span.setAttribute("auth.refresh.wait.outcome", outcome);
        span.end();
      }
    },
  );
}

export async function getOrRefreshAccessToken(recordId: string): Promise<RefreshResult> {
  try {
    const redis = await getRedis();
    const deadline = Date.now() + WAITER_TIMEOUT_MS;
    const existing = await readResult(redis, recordId);
    if (existing) return existing;

    const owner = randomUUID();
    if (await tryAcquireLock(redis, recordId, owner) === "OK") {
      return await refreshUnderLock(redis, recordId, owner);
    }

    const waited = await waitForRefreshResult(redis, recordId, deadline);
    return "result" in waited
      ? waited.result
      : await refreshUnderLock(redis, recordId, waited.owner);
  } catch (error) {
    if (error instanceof RedisRefreshCoordinationError) {
      return refreshWithoutRedis(recordId, error.stage);
    }
    throw error;
  }
}

export async function revokeRefreshSession(recordId: string) {
  const redis = await getRedis();
  // Replace any reusable token with a temporary signed-out marker. A refresh
  // already in flight checks this marker before it may publish its result.
  const previous = await traceAuthDependency("redis", "EVAL", "refresh.revoke", () => redis.eval(SIGN_OUT_SCRIPT, {
    keys: [resultKey(recordId), lockKey(recordId)],
    arguments: [JSON.stringify(failure("RefreshTokenMissing")), String(SIGNED_OUT_TTL_MS)],
  }));
  const result = decodeResult(typeof previous === "string" ? previous : null);
  return result?.status === "success" ? result.accessToken : undefined;
}
