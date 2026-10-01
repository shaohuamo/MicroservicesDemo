import "server-only";

import { context, propagation, SpanKind, SpanStatusCode } from "@opentelemetry/api";
import { normalizeNotificationItem } from "@/lib/notifications/notification-message";
import { getServerTracer, logServerEvent, SeverityNumber } from "@/lib/notifications/server-otel";
import type { NotificationReplayResponse } from "@/types/notification";
import { applyGatewaySessionProof } from "@/lib/auth/gateway-session-proof";

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
    const number = typeof raw === "number" ? raw : NaN;
    if (Number.isSafeInteger(number) && number >= 0) return number;
  }

  return null;
}

export async function fetchNotificationReplayPage({
  accessToken,
  refreshTokenRecordId,
  cursor,
  signal,
}: {
  accessToken: string;
  refreshTokenRecordId: string;
  cursor?: string;
  signal?: AbortSignal;
}): Promise<NotificationReplayResponse> {
  const query = new URLSearchParams({ limit: String(REPLAY_PAGE_SIZE) });
  if (cursor !== undefined) query.set("cursor", cursor);

  const replayUrl = `${getGatewayBaseUrl()}/gateway/notifications/replay?${query.toString()}`;
  const response = await getServerTracer().startActiveSpan(
    "notifications.replay.fetch",
    { kind: SpanKind.CLIENT },
    async (span) => {
      const headers = new Headers({ accept: "application/json", authorization: `Bearer ${accessToken}` });
      applyGatewaySessionProof(headers, refreshTokenRecordId, accessToken);
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
  const watermark = readSafeInteger(record, "watermark", "Watermark");
  const nextCursor = Object.hasOwn(record, "nextCursor") ? record.nextCursor : record.NextCursor;
  if (watermark === null || (nextCursor !== null && (typeof nextCursor !== "string" || !nextCursor))) {
    throw new NotificationGatewayError("Notification replay returned an invalid page cursor or watermark.", 503);
  }
  return { items, nextCursor, watermark };
}
