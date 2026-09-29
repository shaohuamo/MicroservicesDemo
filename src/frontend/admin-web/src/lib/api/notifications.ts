import { api } from "@/lib/api/http-client";
import { normalizeNotificationItem } from "@/lib/notifications/notification-message";
import type { NotificationListResponse } from "@/types/notification";

type NotificationListQuery = {
  beforeSequence?: number;
  limit?: number;
  signal?: AbortSignal;
};

function asRecord(value: unknown): Record<string, unknown> | null {
  return value && typeof value === "object" && !Array.isArray(value)
    ? value as Record<string, unknown>
    : null;
}

function readSafeInteger(
  record: Record<string, unknown>,
  fallback: number,
  ...keys: string[]
) {
  for (const key of keys) {
    const raw = record[key];
    const parsed = typeof raw === "number" ? raw : Number(raw);
    if (Number.isSafeInteger(parsed) && parsed >= 0) return parsed;
  }

  return fallback;
}

function readNullableSequence(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) {
    const raw = record[key];
    if (raw === null || raw === undefined || raw === "") continue;
    const parsed = typeof raw === "number" ? raw : Number(raw);
    if (Number.isSafeInteger(parsed) && parsed >= 0) return parsed;
  }

  return null;
}

function normalizeListResponse(value: unknown): NotificationListResponse {
  const record = asRecord(value) ?? {};
  const rawItems = record.items ?? record.Items;
  const items = Array.isArray(rawItems)
    ? rawItems.map(normalizeNotificationItem).filter((item) => item !== null)
    : [];
  const inferredWatermark = items.reduce(
    (current, item) => Math.max(current, item.sequenceNumber),
    0,
  );

  return {
    items,
    nextBeforeSequence: readNullableSequence(
      record,
      "nextBeforeSequence",
      "NextBeforeSequence",
    ),
    unreadCount: readSafeInteger(
      record,
      items.filter((item) => !item.readAtUtc).length,
      "unreadCount",
      "UnreadCount",
    ),
    watermark: readSafeInteger(
      record,
      inferredWatermark,
      "watermark",
      "Watermark",
    ),
  };
}

export async function getNotifications({
  beforeSequence,
  limit = 20,
  signal,
}: NotificationListQuery = {}): Promise<NotificationListResponse> {
  const params = new URLSearchParams({ limit: String(limit) });
  if (beforeSequence !== undefined) {
    params.set("beforeSequence", String(beforeSequence));
  }

  const { data } = await api.get<unknown>(`/notifications?${params.toString()}`, {
    signal,
  });
  return normalizeListResponse(data);
}

export async function acknowledgeNotification(notificationId: string) {
  await api.post(`/notifications/${encodeURIComponent(notificationId)}/ack`);
}

export async function markNotificationAsRead(notificationId: string) {
  await api.put(`/notifications/${encodeURIComponent(notificationId)}/read`);
}

export async function markAllNotificationsAsRead(upToSequence: number) {
  await api.post("/notifications/read-all", { upToSequence });
}
