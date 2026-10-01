import "server-only";

import { randomUUID } from "node:crypto";
import { createClient } from "redis";
import { SpanKind, SpanStatusCode, context, trace } from "@opentelemetry/api";
import { normalizeRoutedNotification } from "@/lib/notifications/notification-message";
import {
  extractTraceContext,
  getServerTracer,
  injectSpanContext,
  logServerEvent,
  SeverityNumber,
  type TraceContextCarrier,
} from "@/lib/notifications/server-otel";
import type { NotificationItem } from "@/types/notification";

type NotificationRedisCommands = Pick<
  ReturnType<typeof createClient>,
  "expire" | "zAdd" | "zRem" | "isReady"
>;

type LocalConnection = {
  connectionId: string;
  userId: string;
  presenceMember: string;
  replaying: boolean;
  buffered: NotificationItem[];
  presenceReady: boolean;
  realtimeReadyListeners: Set<() => void>;
  send: (notification: NotificationItem, traceContext?: TraceContextCarrier) => void;
};

type NotificationHubState = {
  instanceId: string;
  connectionsByUser: Map<string, Map<string, LocalConnection>>;
  redis?: NotificationRedisCommands;
  subscriber?: { isReady: boolean };
  subscribed: boolean;
  healthy: boolean;
  disconnectGeneration: number;
  startPromise?: Promise<void>;
  restorePromise?: Promise<void>;
};

declare global {
  var adminWebNotificationHub: NotificationHubState | undefined;
}

function createInstanceId() {
  const podName = process.env.BFF_INSTANCE_NAME
    || process.env.POD_NAME
    || process.env.HOSTNAME
    || "admin-web";
  return `${podName}:${randomUUID()}`;
}

function getPresenceTtlSeconds() {
  const configured = Number(process.env.NOTIFICATIONS_PRESENCE_TTL_SECONDS);
  return Number.isFinite(configured) && configured >= 10
    ? Math.floor(configured)
    : 45;
}

function getState() {
  globalThis.adminWebNotificationHub ??= {
    instanceId: createInstanceId(),
    connectionsByUser: new Map(),
    subscribed: false,
    healthy: false,
    disconnectGeneration: 0,
  };

  return globalThis.adminWebNotificationHub;
}

function getRedisUrl() {
  return process.env.NOTIFICATIONS_REDIS_URL
    || process.env.AUTH_REDIS_URL
    || "redis://localhost:6379";
}

function getPresenceKey(userId: string) {
  return `notifications:presence:${userId}`;
}

function getNotificationChannel(instanceId: string) {
  return `notifications:bff:${instanceId}`;
}

function dispatchRedisMessage(serializedMessage: string) {
  let parsed: unknown;

  try {
    parsed = JSON.parse(serializedMessage);
  } catch {
    return;
  }

  const message = normalizeRoutedNotification(parsed);
  if (!message) return;

  const carrier: TraceContextCarrier = {
    traceparent: message.traceParent ?? undefined,
    tracestate: message.traceState ?? undefined,
  };
  const parentContext = extractTraceContext(carrier);
  const consumeSpan = getServerTracer().startSpan(
    "notifications.redis.consume",
    { kind: SpanKind.CONSUMER },
    parentContext,
  );
  consumeSpan.setAttribute("messaging.system", "redis");
  consumeSpan.setAttribute("notification.id", message.notificationId);

  context.with(trace.setSpan(parentContext, consumeSpan), () => {
    try {
      const state = getState();
      const userConnections = state.connectionsByUser.get(message.userId);
      if (!userConnections) {
        logServerEvent(SeverityNumber.INFO, "INFO", "BFF notification received without active connections", {
          "notification.id": message.notificationId,
        });
        return;
      }

  // Re-check the user-to-connection mapping even though the Redis channel is
  // already targeted to this BFF instance. This prevents a stale presence
  // member from routing a notification to the wrong local connection.
  const notification: NotificationItem = {
    notificationId: message.notificationId,
    sequenceNumber: message.sequenceNumber,
    operation: message.operation,
    status: message.status,
    productId: message.productId,
    productName: message.productName,
    occurredAtUtc: message.occurredAtUtc,
    errorCode: message.errorCode,
    ...(message.deliveryStatus ? { deliveryStatus: message.deliveryStatus } : {}),
    ...(message.deliveredAtUtc ? { deliveredAtUtc: message.deliveredAtUtc } : {}),
    readAtUtc: message.readAtUtc ?? null,
  };

      const childContext = injectSpanContext(consumeSpan);
      let sentCount = 0;
      for (const connection of userConnections.values()) {
        if (connection.userId !== message.userId) continue;

        if (connection.replaying) {
          connection.buffered.push(notification);
        } else {
          connection.send(notification, childContext);
        }
        sentCount++;
      }
      logServerEvent(SeverityNumber.INFO, "INFO", "BFF notification dispatched", {
        "notification.id": message.notificationId,
        "notification.connection_count": sentCount,
      });
      consumeSpan.setStatus({ code: SpanStatusCode.OK });
    } catch (error) {
      consumeSpan.recordException(error as Error);
      consumeSpan.setStatus({ code: SpanStatusCode.ERROR });
      logServerEvent(SeverityNumber.ERROR, "ERROR", "BFF notification dispatch failed", {
        "notification.id": message.notificationId,
        "error.type": error instanceof Error ? error.name : "UnknownError",
      });
    } finally {
      consumeSpan.end();
    }
  });
}

function markUnavailable(state: NotificationHubState) {
  let hadDeliveryPath = state.healthy;
  state.healthy = false;
  for (const connections of state.connectionsByUser.values()) {
    for (const connection of connections.values()) {
      hadDeliveryPath ||= connection.presenceReady;
      connection.presenceReady = false;
    }
  }
  if (hadDeliveryPath) state.disconnectGeneration++;
}

function notifyRealtimeReady(state: NotificationHubState) {
  for (const connections of state.connectionsByUser.values()) {
    for (const connection of connections.values()) {
      for (const listener of connection.realtimeReadyListeners) listener();
    }
  }
}

async function writePresence(redis: NotificationRedisCommands, userId: string, member: string) {
  const ttl = getPresenceTtlSeconds();
  const key = getPresenceKey(userId);
  await redis.zAdd(key, { score: Date.now() + ttl * 1_000, value: member });
  await redis.expire(key, ttl);
}

async function restorePresence(state: NotificationHubState) {
  if (!state.subscribed || !state.redis?.isReady || !state.subscriber?.isReady) return;
  state.restorePromise ??= (async () => {
    try {
      for (const [userId, connections] of state.connectionsByUser) {
        for (const connection of connections.values()) {
          await writePresence(state.redis!, userId, connection.presenceMember);
          connection.presenceReady = true;
        }
      }
      if (state.redis?.isReady && state.subscriber?.isReady) {
        state.healthy = true;
        notifyRealtimeReady(state);
      } else {
        markUnavailable(state);
      }
    } catch {
      markUnavailable(state);
    }
  })().finally(() => { state.restorePromise = undefined; });
  await state.restorePromise;
}

async function ensureInfrastructure() {
  const state = getState();

  state.startPromise ??= (async () => {
    const redis = createClient({ url: getRedisUrl() });
    const subscriber = redis.duplicate();
    state.redis = redis;
    state.subscriber = subscriber;

    for (const client of [redis, subscriber]) {
      client.on("error", () => markUnavailable(state));
      client.on("reconnecting", () => markUnavailable(state));
      client.on("end", () => markUnavailable(state));
      client.on("ready", () => { void restorePresence(state); });
    }

    try {
      await Promise.all([redis.connect(), subscriber.connect()]);
      const channel = getNotificationChannel(state.instanceId);
      await getServerTracer().startActiveSpan(
        "redis SUBSCRIBE notifications:bff",
        {
          kind: SpanKind.CLIENT,
          attributes: {
            "db.system.name": "redis",
            "db.operation.name": "SUBSCRIBE",
            "messaging.system": "redis",
            "messaging.operation.name": "subscribe",
            "messaging.destination.name": channel,
          },
        },
        async (span) => {
          try {
            await subscriber.subscribe(channel, dispatchRedisMessage);
            state.subscribed = true;
            span.setStatus({ code: SpanStatusCode.OK });
          } catch (error) {
            span.setAttribute("error.type", error instanceof Error ? error.name : "UnknownError");
            span.setStatus({ code: SpanStatusCode.ERROR });
            throw error;
          } finally {
            span.end();
          }
        },
      );
      await restorePresence(state);
    } catch (error) {
      markUnavailable(state);
      if (redis.isOpen) redis.destroy();
      if (subscriber.isOpen) subscriber.destroy();
      state.redis = undefined;
      state.subscriber = undefined;
      state.subscribed = false;
      state.startPromise = undefined;
      throw error;
    }
  })();

  await state.startPromise;
}

export type NotificationConnection = {
  connectionId: string;
  activateLiveDelivery: () => NotificationItem[];
  refreshPresence: () => Promise<void>;
  isRealtimeAvailable: () => boolean;
  waitForRealtimeReady: (timeoutMs: number, signal: AbortSignal) => Promise<boolean>;
  getDisconnectGeneration: () => number;
  close: () => Promise<void>;
};

export async function registerNotificationConnection(
  userId: string,
  send: (notification: NotificationItem) => void,
): Promise<NotificationConnection> {
  const state = getState();
  const subscriptionAction = state.redis
    ? "reuse"
    : state.startPromise
      ? "await"
      : "initialize";
  void getServerTracer().startActiveSpan(
    "notifications.redis.subscription",
    {
      kind: SpanKind.INTERNAL,
      attributes: {
        "messaging.system": "redis",
        "messaging.destination.name": getNotificationChannel(state.instanceId),
        "notification.redis.subscription.action": subscriptionAction,
      },
    },
    async (span) => {
      try {
        await ensureInfrastructure();
        span.setStatus({ code: SpanStatusCode.OK });
      } catch (error) {
        span.setAttribute("error.type", error instanceof Error ? error.name : "UnknownError");
        span.setStatus({ code: SpanStatusCode.ERROR });
      } finally {
        span.end();
      }
    },
  );
  const connectionId = randomUUID();
  const presenceMember = `${state.instanceId}:${connectionId}`;
  const connection: LocalConnection = {
    connectionId,
    userId,
    presenceMember,
    replaying: true,
    buffered: [],
    presenceReady: false,
    realtimeReadyListeners: new Set(),
    send,
  };
  const userConnections = state.connectionsByUser.get(userId) ?? new Map();
  userConnections.set(connectionId, connection);
  state.connectionsByUser.set(userId, userConnections);

  let closed = false;

  function isRealtimeAvailable() {
    return Boolean(!closed && state.healthy && state.subscribed
      && state.redis?.isReady && state.subscriber?.isReady && connection.presenceReady);
  }

  function waitForRealtimeReady(timeoutMs: number, signal: AbortSignal): Promise<boolean> {
    if (signal.aborted) {
      return Promise.reject(signal.reason ?? new DOMException("The request was aborted.", "AbortError"));
    }
    if (closed) return Promise.resolve(false);
    if (isRealtimeAvailable()) return Promise.resolve(true);

    return new Promise<boolean>((resolve, reject) => {
      let settled = false;
      let timeout: ReturnType<typeof setTimeout>;

      function cleanupWait() {
        clearTimeout(timeout);
        signal.removeEventListener("abort", handleWaitAbort);
        connection.realtimeReadyListeners.delete(handleReady);
      }

      function finish(ready: boolean) {
        if (settled) return;
        settled = true;
        cleanupWait();
        resolve(ready);
      }

      function handleReady() {
        if (closed) finish(false);
        else if (isRealtimeAvailable()) finish(true);
      }

      function handleWaitAbort() {
        if (settled) return;
        settled = true;
        cleanupWait();
        reject(signal.reason ?? new DOMException("The request was aborted.", "AbortError"));
      }

      connection.realtimeReadyListeners.add(handleReady);
      signal.addEventListener("abort", handleWaitAbort, { once: true });
      timeout = setTimeout(() => finish(false), timeoutMs);
      handleReady();
    });
  }

  async function refreshPresence() {
    if (closed) return;
    const redis = state.redis;
    if (!redis?.isReady || !state.subscriber?.isReady || !state.subscribed) {
      connection.presenceReady = false;
      if (!state.startPromise) void ensureInfrastructure().catch(() => undefined);
      return;
    }
    try {
      await writePresence(redis, userId, presenceMember);
      connection.presenceReady = true;
      if (!state.healthy) await restorePresence(state);
      for (const listener of connection.realtimeReadyListeners) listener();
    } catch {
      markUnavailable(state);
    }
  }

  async function close() {
    if (closed) return;
    closed = true;
    for (const listener of connection.realtimeReadyListeners) listener();

    const currentConnections = state.connectionsByUser.get(userId);
    currentConnections?.delete(connectionId);
    if (currentConnections?.size === 0) {
      state.connectionsByUser.delete(userId);
    }

    try {
      if (state.redis?.isReady) {
        await state.redis.zRem(getPresenceKey(userId), presenceMember);
      }
    } catch {
      // The 45-second score/TTL is the crash-safe cleanup path when Redis is
      // unavailable during disconnect.
    }
  }

  void refreshPresence();

  return {
    connectionId,
    refreshPresence,
    isRealtimeAvailable,
    waitForRealtimeReady,
    getDisconnectGeneration: () => state.disconnectGeneration,
    activateLiveDelivery() {
      connection.replaying = false;
      const buffered = connection.buffered;
      connection.buffered = [];
      return buffered.sort((left, right) => left.sequenceNumber - right.sequenceNumber);
    },
    close,
  };
}

export function getNotificationBffInstanceId() {
  return getState().instanceId;
}
