import { auth } from "@/auth";
import type { NextRequest } from "next/server";
import { context, SpanKind, SpanStatusCode, trace } from "@opentelemetry/api";
import {
  registerNotificationConnection,
  type NotificationConnection,
} from "@/lib/notifications/server-hub";
import {
  fetchNotificationReplayPage,
  NotificationGatewayError,
} from "@/lib/notifications/server-gateway";
import {
  extractTraceContext,
  getServerTracer,
  injectSpanContext,
  logServerEvent,
  resolveRequestTraceContext,
  SeverityNumber,
  type TraceContextCarrier,
} from "@/lib/notifications/server-otel";
import type { NotificationReplayResponse, NotificationItem } from "@/types/notification";
import { getRefreshTokenRecordId } from "@/lib/auth/gateway-session-proof";
import { getOrRefreshAccessToken } from "@/lib/auth/refresh-coordinator";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const encoder = new TextEncoder();
const FALLBACK_POLL_INTERVAL_MS = 5_000;
const REALTIME_READY_TIMEOUT_MS = 1_000;

function accessTokenExpiresAt(token: string) {
  try {
    const payload = JSON.parse(Buffer.from(token.split(".")[1], "base64url").toString("utf8"));
    return typeof payload.exp === "number" ? payload.exp * 1_000 : 0;
  } catch {
    return 0;
  }
}

function getHeartbeatIntervalMs() {
  const seconds = Number(process.env.NOTIFICATIONS_SSE_HEARTBEAT_SECONDS);
  return (Number.isFinite(seconds) && seconds >= 1 ? seconds : 15) * 1_000;
}

function occurredAtMicroseconds(value: string): bigint | null {
  const milliseconds = Date.parse(value);
  if (!Number.isFinite(milliseconds)) return null;

  // Date.parse retains only milliseconds. Preserve the remaining PostgreSQL
  // timestamp precision, including ISO timestamps carrying a UTC offset.
  const fraction = value.match(/\.(\d+)(?:Z|[+-]\d{2}:\d{2})$/i)?.[1] ?? "";
  const microseconds = fraction.padEnd(6, "0").slice(3, 6);
  return BigInt(milliseconds) * BigInt(1_000) + BigInt(microseconds);
}

function compareNotificationsOldestFirst(left: NotificationItem, right: NotificationItem) {
  const leftTime = occurredAtMicroseconds(left.occurredAtUtc);
  const rightTime = occurredAtMicroseconds(right.occurredAtUtc);
  if (leftTime !== null && rightTime !== null) {
    if (leftTime < rightTime) return -1;
    if (leftTime > rightTime) return 1;
  }
  return left.sequenceNumber - right.sequenceNumber;
}

function formatNotificationEvent(
  notification: NotificationItem,
  traceContext?: TraceContextCarrier,
) {
  const browserMessage = {
    notificationId: notification.notificationId,
    sequenceNumber: notification.sequenceNumber,
    operation: notification.operation,
    status: notification.status,
    productId: notification.productId,
    productName: notification.productName,
    occurredAtUtc: notification.occurredAtUtc,
    errorCode: notification.errorCode,
    ...(traceContext?.traceparent ? { traceparent: traceContext.traceparent } : {}),
    ...(traceContext?.tracestate ? { tracestate: traceContext.tracestate } : {}),
  };

  return encoder.encode(
    `id: ${notification.sequenceNumber}\nevent: notification\ndata: ${JSON.stringify(browserMessage)}\n\n`,
  );
}

async function handleGet(request: NextRequest) {
  const refreshTokenRecordId = await getRefreshTokenRecordId(request);
  const session = await auth();
  const userId = session?.userId;

  if (!session || session.error || !userId || !refreshTokenRecordId) {
    return Response.json(
      { message: "Authentication is required." },
      { status: 401 },
    );
  }

  let streamController: ReadableStreamDefaultController<Uint8Array> | null = null;
  let connection: NotificationConnection;
  let closed = false;
  let heartbeat: ReturnType<typeof setInterval> | undefined;
  let fallbackTimer: ReturnType<typeof setInterval> | undefined;

  async function cleanup() {
    if (closed) return;
    closed = true;
    if (heartbeat) clearInterval(heartbeat);
    if (fallbackTimer) clearInterval(fallbackTimer);
    request.signal.removeEventListener("abort", handleAbort);
    await connection.close();
  }

  function closeStream() {
    if (!streamController) return;
    try {
      streamController.close();
    } catch {
      // The client may already have cancelled the stream.
    }
  }

  function handleAbort() {
    void cleanup();
    closeStream();
  }

  const sendNotification = (
    notification: NotificationItem,
    parentTraceContext?: TraceContextCarrier,
  ) => {
    const controller = streamController;
    if (closed || !controller) return;

    const parentContext = extractTraceContext(parentTraceContext ?? {});
    const span = getServerTracer().startSpan(
      "notifications.sse.write",
      { kind: SpanKind.PRODUCER },
      parentContext,
    );
    span.setAttribute("notification.id", notification.notificationId);
    context.with(trace.setSpan(parentContext, span), () => {
      try {
        controller.enqueue(formatNotificationEvent(notification, injectSpanContext(span)));
        span.setStatus({ code: SpanStatusCode.OK });
        logServerEvent(SeverityNumber.INFO, "INFO", "BFF notification sent to SSE client", {
          "notification.id": notification.notificationId,
          "notification.operation": notification.operation,
        });
      } catch (error) {
        span.setStatus({ code: SpanStatusCode.ERROR });
        logServerEvent(SeverityNumber.ERROR, "ERROR", "BFF notification SSE write failed", {
          "notification.id": notification.notificationId,
          "error.type": error instanceof Error ? error.name : "UnknownError",
        });
        void cleanup();
      } finally {
        span.end();
      }
    });
  };

  try {
    // Register locally before replay. Redis setup and presence registration run
    // asynchronously; live messages received during replay are buffered.
    connection = await registerNotificationConnection(userId, sendNotification);
  } catch {
    return Response.json(
      { message: "The realtime notification channel is unavailable." },
      { status: 503 },
    );
  }

  async function replayWithAccessToken(accessToken: string) {
    let currentAccessToken = accessToken;
    let tokenExpiresAt = accessTokenExpiresAt(accessToken);

    async function refreshedAccessToken(force = false) {
      if (!force && tokenExpiresAt > Date.now() + 60_000) return currentAccessToken;
      const result = await getOrRefreshAccessToken(refreshTokenRecordId!);
      if (result.status === "failure") {
        throw new NotificationGatewayError(
          result.error,
          result.error === "RefreshUnavailable" ? 503 : 401,
        );
      }
      currentAccessToken = result.accessToken;
      tokenExpiresAt = result.expiresAt * 1_000;
      return currentAccessToken;
    }

    async function fetchFallbackPage(cursor?: string) {
      const requestPage = (token: string) => fetchNotificationReplayPage({
        accessToken: token,
        refreshTokenRecordId: refreshTokenRecordId!,
        cursor,
        signal: request.signal,
      });
      try {
        return await requestPage(await refreshedAccessToken());
      } catch (error) {
        if (!(error instanceof NotificationGatewayError) || error.status !== 401) throw error;
        return requestPage(await refreshedAccessToken(true));
      }
    }

    async function scanUndelivered(
      firstPage?: NotificationReplayResponse,
      sentNotificationIds = new Set<string>(),
    ) {
      let page = firstPage ?? await fetchFallbackPage();
      const watermark = page.watermark;
      const visitedCursors = new Set<string>();
      while (!closed) {
        if (page.watermark !== watermark) throw new Error("Replay watermark changed during pagination.");
        for (const notification of page.items) {
          if (closed) return;
          if (notification.sequenceNumber > watermark) throw new Error("Notification exceeded the replay watermark.");
          if (sentNotificationIds.has(notification.notificationId)) continue;
          sendNotification(notification);
          sentNotificationIds.add(notification.notificationId);
        }
        const next = page.nextCursor;
        if (next === null) return;
        if (visitedCursors.has(next)) throw new Error("Replay cursor did not advance.");
        visitedCursors.add(next);
        page = await fetchFallbackPage(next);
      }
    }

    // Let normal subscription/presence setup finish before taking the replay
    // boundary. A timeout keeps the existing Redis fallback path available.
    const realtimeReady = await connection.waitForRealtimeReady(REALTIME_READY_TIMEOUT_MS, request.signal);
    request.signal.throwIfAborted();
    const initialRealtimeAvailable = realtimeReady && connection.isRealtimeAvailable();
    const initialDisconnectGeneration = connection.getDisconnectGeneration();
    let firstReplayPage: NotificationReplayResponse;
    try {
      firstReplayPage = await fetchNotificationReplayPage({
        accessToken, refreshTokenRecordId: refreshTokenRecordId!, signal: request.signal,
      });
    } catch (error) {
      await connection.close();
      const status = error instanceof NotificationGatewayError ? error.status : 503;
      return Response.json(
        { message: "Notification replay is unavailable." },
        { status: status === 401 || status === 403 ? status : 503 },
      );
    }

    request.signal.addEventListener("abort", handleAbort, { once: true });

    const stream = new ReadableStream<Uint8Array>({
      async start(controller) {
        streamController = controller;
        controller.enqueue(encoder.encode("retry: 3000\n: connected\n\n"));

        const sentNotificationIds = new Set<string>();
        try {
          await scanUndelivered(firstReplayPage, sentNotificationIds);
          if (closed) return;

          // Business timestamps and persistence sequences can arrive out of order.
          // Only notification identity proves that a buffered message was replayed.
          const buffered = connection.activateLiveDelivery().sort(compareNotificationsOldestFirst);
          for (const notification of buffered) {
            if (sentNotificationIds.has(notification.notificationId)) continue;
            sendNotification(notification);
            sentNotificationIds.add(notification.notificationId);
          }

          let refreshingPresence = false;
          heartbeat = setInterval(() => {
            if (closed || refreshingPresence || !streamController) return;
            refreshingPresence = true;

            void connection.refreshPresence()
              .catch(() => undefined)
              .finally(() => {
                refreshingPresence = false;
              });

            try {
              streamController.enqueue(
                encoder.encode(`: heartbeat ${new Date().toISOString()}\n\n`),
              );
            } catch {
              void cleanup();
            }
          }, getHeartbeatIntervalMs());

          let fallbackRunning = false;
          let needsFinalScan = !initialRealtimeAvailable || !connection.isRealtimeAvailable()
            || connection.getDisconnectGeneration() !== initialDisconnectGeneration;
          let observedDisconnectGeneration = connection.getDisconnectGeneration();
          async function pollWhenRedisUnavailable() {
            if (closed || fallbackRunning) return;
            const disconnectGeneration = connection.getDisconnectGeneration();
            if (disconnectGeneration !== observedDisconnectGeneration) {
              observedDisconnectGeneration = disconnectGeneration;
              needsFinalScan = true;
            }
            if (!connection.isRealtimeAvailable()) needsFinalScan = true;
            if (!needsFinalScan) return;

            fallbackRunning = true;
            const scanStartedWithRealtime = connection.isRealtimeAvailable();
            const scanGeneration = connection.getDisconnectGeneration();
            try {
              await scanUndelivered();
              if (scanStartedWithRealtime
                && connection.isRealtimeAvailable()
                && connection.getDisconnectGeneration() === scanGeneration) {
                needsFinalScan = false;
              }
            } catch (error) {
              logServerEvent(SeverityNumber.WARN, "WARN", "BFF notification fallback scan failed", {
                "error.type": error instanceof Error ? error.name : "UnknownError",
              });
              if (error instanceof NotificationGatewayError
                && (error.status === 401 || error.status === 403)) {
                await cleanup();
                closeStream();
              }
            } finally {
              fallbackRunning = false;
            }
          }

          fallbackTimer = setInterval(() => { void pollWhenRedisUnavailable(); }, FALLBACK_POLL_INTERVAL_MS);
        } catch {
          if (!closed) {
            try {
              controller.enqueue(
                encoder.encode("event: server-error\ndata: {}\n\n"),
              );
            } catch {
              // The client already disconnected.
            }
          }

          await cleanup();
          closeStream();
        }
      },
      async cancel() {
        await cleanup();
      },
    });

    return new Response(stream, {
      headers: {
        "cache-control": "no-cache, no-store, no-transform",
        connection: "keep-alive",
        "content-type": "text/event-stream; charset=utf-8",
        "x-accel-buffering": "no",
      },
    });
  }

  const authenticatedReplay = await auth(async (authenticatedRequest) => {
    const accessToken = authenticatedRequest.auth?.accessToken;
    if (authenticatedRequest.auth?.error === "RefreshUnavailable") {
      await cleanup();
      return Response.json(
        { message: "Authentication refresh is temporarily unavailable." },
        { status: 503, headers: { "retry-after": "1" } },
      );
    }
    if (!authenticatedRequest.auth || authenticatedRequest.auth.error || !accessToken) {
      await cleanup();
      return Response.json(
        { message: "Authentication is required." },
        { status: 401 },
      );
    }

    return replayWithAccessToken(accessToken);
  });

  try {
    return await authenticatedReplay(request, { params: Promise.resolve({}) });
  } catch (error) {
    await cleanup();
    throw error;
  }
}

export async function GET(request: NextRequest) {
  const parentContext = resolveRequestTraceContext({
    traceparent: request.headers.get("traceparent") ?? undefined,
    tracestate: request.headers.get("tracestate") ?? undefined,
  });

  return context.with(parentContext, () => getServerTracer().startActiveSpan(
    "notifications.sse.setup",
    {
      kind: SpanKind.INTERNAL,
      attributes: {
        "http.request.method": "GET",
        "http.route": "/api/notifications/stream",
      },
    },
    async (span) => {
      try {
        const response = await handleGet(request);
        if (response) span.setAttribute("http.response.status_code", response.status);
        span.setStatus({
          code: response && response.status >= 500 ? SpanStatusCode.ERROR : SpanStatusCode.OK,
        });
        return response;
      } catch (error) {
        span.setAttribute("error.type", error instanceof Error ? error.name : "UnknownError");
        span.setStatus({ code: SpanStatusCode.ERROR });
        throw error;
      } finally {
        // The SSE response stays open, but setup and the initial replay are done.
        span.end();
      }
    },
  ));
}
