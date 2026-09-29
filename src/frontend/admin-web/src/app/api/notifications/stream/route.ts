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
import type { NotificationItem } from "@/types/notification";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const encoder = new TextEncoder();

function getHeartbeatIntervalMs() {
  const seconds = Number(process.env.NOTIFICATIONS_SSE_HEARTBEAT_SECONDS);
  return (Number.isFinite(seconds) && seconds >= 1 ? seconds : 15) * 1_000;
}

function parseSequence(value: string | null) {
  if (!value || !/^\d+$/.test(value)) return 0;
  const sequence = Number(value);
  return Number.isSafeInteger(sequence) ? sequence : 0;
}

function getReplayCursor(request: Request) {
  const headerCursor = parseSequence(request.headers.get("last-event-id"));
  if (headerCursor > 0) return headerCursor;

  return parseSequence(new URL(request.url).searchParams.get("afterSequence"));
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
  const session = await auth();
  const userId = session?.userId;

  if (!session || session.error || !userId) {
    return Response.json(
      { message: "Authentication is required." },
      { status: 401 },
    );
  }

  const afterSequence = getReplayCursor(request);
  let streamController: ReadableStreamDefaultController<Uint8Array> | null = null;
  let connection: NotificationConnection;
  let closed = false;
  let heartbeat: ReturnType<typeof setInterval> | undefined;

  async function cleanup() {
    if (closed) return;
    closed = true;
    if (heartbeat) clearInterval(heartbeat);
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
    // Subscription is established before presence is registered. Messages that
    // arrive while the durable replay is running are held by this connection.
    connection = await registerNotificationConnection(userId, sendNotification);
  } catch {
    return Response.json(
      { message: "The realtime notification channel is unavailable." },
      { status: 503 },
    );
  }

  async function replayWithAccessToken(accessToken: string) {
    let firstReplayPage;
    try {
      firstReplayPage = await fetchNotificationReplayPage({
        accessToken,
        afterSequence,
        signal: request.signal,
      });
    } catch (error) {
      await connection.close();
      const status = error instanceof NotificationGatewayError
        ? error.status
        : 503;

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
        let lastSentSequence = afterSequence;
        let replayCursor = afterSequence;
        let page = firstReplayPage;
        const replayWatermark = firstReplayPage.watermark;

        try {
          // The watermark remains fixed across pages so a hot notification stream
          // cannot make replay infinite or create a replay/live race.
          for (let pageNumber = 0; pageNumber < 10_000 && !closed; pageNumber++) {
            for (const notification of page.items) {
              if (
                notification.sequenceNumber <= afterSequence
                || notification.sequenceNumber > replayWatermark
                || sentNotificationIds.has(notification.notificationId)
              ) {
                continue;
              }

              sendNotification(notification);
              sentNotificationIds.add(notification.notificationId);
              lastSentSequence = Math.max(lastSentSequence, notification.sequenceNumber);
            }

            const next = page.nextAfterSequence;
            if (next === null || next <= replayCursor || next >= replayWatermark) {
              break;
            }

            replayCursor = next;
            page = await fetchNotificationReplayPage({
              accessToken,
              afterSequence: next,
              upToSequence: replayWatermark,
              signal: request.signal,
            });
          }

          // Switching the connection to live and taking its buffer is synchronous,
          // so Redis delivery cannot fall into a gap between those two operations.
          const buffered = connection.activateLiveDelivery();
          for (const notification of buffered) {
            if (
              notification.sequenceNumber <= lastSentSequence
              || sentNotificationIds.has(notification.notificationId)
            ) {
              continue;
            }

            sendNotification(notification);
            sentNotificationIds.add(notification.notificationId);
            lastSentSequence = notification.sequenceNumber;
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
