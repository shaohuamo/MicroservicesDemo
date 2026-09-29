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
  "expire" | "zAdd" | "zRem"
>;

type LocalConnection = {
  connectionId: string;
  userId: string;
  presenceMember: string;
  replaying: boolean;
  buffered: NotificationItem[];
  send: (notification: NotificationItem, traceContext?: TraceContextCarrier) => void;
};

type NotificationHubState = {
  instanceId: string;
  connectionsByUser: Map<string, Map<string, LocalConnection>>;
  redis?: NotificationRedisCommands;
  subscriber?: unknown;
  startPromise?: Promise<void>;
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

async function ensureInfrastructure() {
  const state = getState();

  state.startPromise ??= (async () => {
    const redis = createClient({ url: getRedisUrl() });
    const subscriber = redis.duplicate();

    // node-redis reports background reconnect errors through this event. The
    // active command still rejects, allowing the route to fail safely.
    redis.on("error", () => undefined);
    subscriber.on("error", () => undefined);

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
      state.redis = redis;
      state.subscriber = subscriber;
    } catch (error) {
      if (redis.isOpen) redis.destroy();
      if (subscriber.isOpen) subscriber.destroy();
      state.startPromise = undefined;
      throw error;
    }
  })();

  await state.startPromise;

  if (!state.redis) {
    throw new Error("Notification Redis connection is unavailable.");
  }

  return state.redis;
}

export type NotificationConnection = {
  connectionId: string;
  activateLiveDelivery: () => NotificationItem[];
  refreshPresence: () => Promise<void>;
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
  const redis = await getServerTracer().startActiveSpan(
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
        const client = await ensureInfrastructure();
        span.setStatus({ code: SpanStatusCode.OK });
        return client;
      } catch (error) {
        span.setAttribute("error.type", error instanceof Error ? error.name : "UnknownError");
        span.setStatus({ code: SpanStatusCode.ERROR });
        throw error;
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
    send,
  };
  const userConnections = state.connectionsByUser.get(userId) ?? new Map();
  userConnections.set(connectionId, connection);
  state.connectionsByUser.set(userId, userConnections);

  let closed = false;

  async function refreshPresence() {
    if (closed) return;

    const presenceTtlSeconds = getPresenceTtlSeconds();
    const expiresAt = Date.now() + presenceTtlSeconds * 1_000;
    const key = getPresenceKey(userId);
    await redis.zAdd(key, { score: expiresAt, value: presenceMember });
    await redis.expire(key, presenceTtlSeconds);
  }

  async function close() {
    if (closed) return;
    closed = true;

    const currentConnections = state.connectionsByUser.get(userId);
    currentConnections?.delete(connectionId);
    if (currentConnections?.size === 0) {
      state.connectionsByUser.delete(userId);
    }

    try {
      await redis.zRem(getPresenceKey(userId), presenceMember);
    } catch {
      // The 45-second score/TTL is the crash-safe cleanup path when Redis is
      // unavailable during disconnect.
    }
  }

  try {
    await refreshPresence();
  } catch (error) {
    await close();
    throw error;
  }

  return {
    connectionId,
    refreshPresence,
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
