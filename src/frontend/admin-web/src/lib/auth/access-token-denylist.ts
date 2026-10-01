type RedisModule = typeof import("redis");
type RedisClient = {
  isReady: boolean;
  connect: () => Promise<unknown>;
  on: (event: "error", listener: (error: unknown) => void) => unknown;
  set: (key: string, value: string, options: { EX: number }) => Promise<unknown>;
};

declare global {
  // eslint-disable-next-line no-var
  var adminWebAccessTokenDenylistRedis: RedisClient | undefined;
}

type JwtPayload = {
  jti?: string;
  exp?: number;
};

function getRedisUrl() {
  return process.env.AUTH_REDIS_URL || "redis://localhost:6379";
}

function getDenylistKey(jti: string) {
  const prefix = process.env.AUTH_ACCESS_TOKEN_DENYLIST_PREFIX || "admin-web:access-token-denylist";
  return `${prefix}:${jti}`;
}

async function getRedisClient() {
  const existing = globalThis.adminWebAccessTokenDenylistRedis;
  if (existing?.isReady) return existing;
  globalThis.adminWebAccessTokenDenylistRedis = undefined;

  const { createClient } = await import("redis") as RedisModule;
  const client = createClient({
    url: getRedisUrl(),
    disableOfflineQueue: true,
    socket: { connectTimeout: 1_000, reconnectStrategy: false },
  });

  client.on("error", () => {
    // Redis command failures are surfaced through rejected promises below.
  });

  await client.connect();
  globalThis.adminWebAccessTokenDenylistRedis = client;
  return client;
}

function base64UrlDecode(value: string) {
  const base64 = value.replace(/-/g, "+").replace(/_/g, "/");
  const padded = base64.padEnd(base64.length + (4 - base64.length % 4) % 4, "=");

  return Buffer.from(padded, "base64").toString("utf8");
}

function decodeJwtPayload(accessToken: string): JwtPayload | null {
  const [, payload] = accessToken.split(".");

  if (!payload) {
    return null;
  }

  try {
    return JSON.parse(base64UrlDecode(payload)) as JwtPayload;
  } catch {
    return null;
  }
}

export async function denylistAccessToken(accessToken: string) {
  const ttlSeconds = getAccessTokenDenylistTtlSeconds(accessToken);
  if (ttlSeconds <= 0) return;
  const payload = decodeJwtPayload(accessToken)!;
  const redis = await getRedisClient();

  try {
    await redis.set(getDenylistKey(payload.jti!), "1", {
      EX: ttlSeconds,
    });
  } catch (error) {
    if (globalThis.adminWebAccessTokenDenylistRedis === redis) {
      globalThis.adminWebAccessTokenDenylistRedis = undefined;
    }
    throw error;
  }
}

export function getAccessTokenDenylistTtlSeconds(accessToken: string, now = Math.floor(Date.now() / 1000)) {
  const payload = decodeJwtPayload(accessToken);

  if (!payload?.jti || !payload.exp || payload.exp + 30 <= now) {
    return 0;
  }

  return payload.exp + 30 - now;
}
