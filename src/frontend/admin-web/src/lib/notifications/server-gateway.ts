import "server-only";

import { context, propagation, SpanKind, SpanStatusCode } from "@opentelemetry/api";
import { normalizeNotificationItem } from "@/lib/notifications/notification-message";
import { getServerTracer, logServerEvent, SeverityNumber } from "@/lib/notifications/server-otel";
import type { NotificationReplayResponse } from "@/types/notification";

const REPLAY_PAGE_SIZE = 100;

export class NotificationGatewayError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
    this.name = "NotificationGatewayError";
  }
}

function getGatewayBaseUrl() {
  const value = process.env.API_GATEWAY_INTERNAL_URL;
  if (!value) {
    throw new NotificationGatewayError(
      "API_GATEWAY_INTERNAL_URL is not configured.",
      500,
    );
  }

  return value.endsWith("/") ? value.slice(0, -1) : value;
}

function asRecord(value: unknown): Record<string, unknown> | null {
  return value && typeof value === "object" && !Array.isArray(value)
    ? value as Record<string, unknown>
    : null;
}

function readSafeInteger(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const raw = record[key];
    const number = typeof raw === "number" ? raw : Number(raw);
    if (Number.isSafeInteger(number) && number >= 0) return number;
  }

  return null;
}

export async function fetchNotificationReplayPage({
  accessToken,
  afterSequence,
  upToSequence,
  signal,
}: {
  accessToken: string;
  afterSequence: number;
  upToSequence?: number;
  signal?: AbortSignal;
}): Promise<NotificationReplayResponse> {
  const query = new URLSearchParams({
    afterSequence: String(afterSequence),
    limit: String(REPLAY_PAGE_SIZE),
  });

  if (upToSequence !== undefined) {
    query.set("upToSequence", String(upToSequence));
  }

  const replayUrl = `${getGatewayBaseUrl()}/gateway/notifications/replay?${query.toString()}`;
  const response = await getServerTracer().startActiveSpan(
    "notifications.replay.fetch",
    { kind: SpanKind.CLIENT },
    async (span) => {
      const headers = new Headers({ accept: "application/json", authorization: `Bearer ${accessToken}` });
      span.setAttribute("http.request.method", "GET");
      span.setAttribute("http.route", "/gateway/notifications/replay");
      propagation.inject(context.active(), headers, {
        set(carrier, key, value) {
          carrier.set(key, value);
        },
      });
      logServerEvent(SeverityNumber.INFO, "INFO", "BFF notification replay request started", {
        "http.request.method": "GET",
        "http.route": "/gateway/notifications/replay",
      });

      try {
        const replayResponse = await fetch(replayUrl, {
          headers,
          cache: "no-store",
          redirect: "manual",
          signal,
        });
        span.setAttribute("http.response.status_code", replayResponse.status);
        span.setStatus({
          code: replayResponse.ok ? SpanStatusCode.OK : SpanStatusCode.ERROR,
        });
        logServerEvent(
          replayResponse.ok ? SeverityNumber.INFO : SeverityNumber.WARN,
          replayResponse.ok ? "INFO" : "WARN",
          "BFF notification replay request completed",
          {
            "http.request.method": "GET",
            "http.route": "/gateway/notifications/replay",
            "http.response.status_code": replayResponse.status,
          },
        );
        return replayResponse;
      } catch (error) {
        span.recordException(error instanceof Error ? error : new Error(String(error)));
        span.setStatus({ code: SpanStatusCode.ERROR });
        logServerEvent(SeverityNumber.ERROR, "ERROR", "BFF notification replay request failed", {
          "http.request.method": "GET",
          "http.route": "/gateway/notifications/replay",
          "error.type": error instanceof Error ? error.name : "UnknownError",
        });
        throw error;
      } finally {
        span.end();
      }
    },
  );

  if (!response.ok) {
    throw new NotificationGatewayError(
      `Notification replay request failed with status ${response.status}.`,
      response.status,
    );
  }

  const value = await response.json() as unknown;
  const record = asRecord(value) ?? {};
  const rawItems = record.items ?? record.Items;
  const items = Array.isArray(rawItems)
    ? rawItems.map(normalizeNotificationItem).filter((item) => item !== null)
    : [];
  const inferredWatermark = items.reduce(
    (current, item) => Math.max(current, item.sequenceNumber),
    afterSequence,
  );
  const watermark = readSafeInteger(record, "watermark", "Watermark")
    ?? upToSequence
    ?? inferredWatermark;
  const explicitNext = readSafeInteger(
    record,
    "nextAfterSequence",
    "NextAfterSequence",
  );
  const nextAfterSequence = explicitNext
    ?? (items.length === REPLAY_PAGE_SIZE
      ? items.at(-1)?.sequenceNumber ?? null
      : null);

  return {
    items,
    nextAfterSequence,
    watermark,
  };
}
