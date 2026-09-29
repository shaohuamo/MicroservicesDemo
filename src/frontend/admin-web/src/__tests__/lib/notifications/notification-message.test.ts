import { describe, expect, it, vi } from "vitest";
import {
  normalizeNotificationItem,
  normalizeRoutedNotification,
} from "@/lib/notifications/notification-message";
import {
  getNotificationDetail,
  getNotificationTitle,
} from "@/lib/notifications/presentation";
import type { NotificationItem } from "@/types/notification";

const notification: NotificationItem = {
  notificationId: "0199f05d-e23a-7d2e-93e4-112233445566",
  sequenceNumber: 42,
  operation: "Update",
  status: "Failure",
  productId: "4ff5f86d-bca9-4d9d-97fc-026aa497a2e8",
  productName: "Keyboard",
  occurredAtUtc: "2026-09-13T08:00:00Z",
  errorCode: "product.update.not_found",
};

describe("notification message normalization", () => {
  it("accepts browser-safe PascalCase API payloads", () => {
    expect(normalizeNotificationItem({
      NotificationId: notification.notificationId,
      SequenceNumber: "42",
      Operation: "update",
      Status: "failed",
      ProductName: "Keyboard",
      OccurredAtUtc: notification.occurredAtUtc,
    })).toMatchObject({
      notificationId: notification.notificationId,
      sequenceNumber: 42,
      operation: "Update",
      status: "Failure",
    });
  });

  it("rejects malformed events and routed events without a user", () => {
    expect(normalizeNotificationItem({ ...notification, sequenceNumber: -1 })).toBeNull();
    expect(normalizeRoutedNotification(notification)).toBeNull();
  });

  it("keeps UserId only in the Redis routing envelope", () => {
    const routed = normalizeRoutedNotification({ ...notification, userId: "user-1" });
    expect(routed?.userId).toBe("user-1");
    const browserEvent = normalizeNotificationItem(routed);
    expect(browserEvent).not.toHaveProperty("userId");
    expect(browserEvent).not.toHaveProperty("userEmail");
  });
});

describe("notification presentation", () => {
  it("selects localized keys from operation, status, and safe error code", () => {
    const translate = vi.fn((key: string) => key);
    expect(getNotificationTitle(notification, translate)).toBe(
      "notifications.title.update.failure",
    );
    expect(getNotificationDetail(notification, translate)).toBe(
      "notifications.error.notFound",
    );
  });
});
