export type ProductOperation = "Add" | "Update" | "Delete";

export type ProductOperationStatus = "Success" | "Failure";

export type NotificationDeliveryStatus =
  | "Pending"
  | "AwaitingSseAck"
  | "RetryScheduled"
  | "SendingEmail"
  | "DeliveredInApp"
  | "DeliveredEmail"
  | "Failed";

/**
 * The browser-safe representation returned by NotificationsMicroservice.
 * User identifiers, email addresses, and internal exception details must never
 * be included in this DTO.
 */
export type NotificationItem = {
  notificationId: string;
  sequenceNumber: number;
  operation: ProductOperation;
  status: ProductOperationStatus;
  productId: string | null;
  productName: string | null;
  occurredAtUtc: string;
  errorCode: string | null;
  deliveryStatus?: NotificationDeliveryStatus;
  deliveredAtUtc?: string | null;
  readAtUtc?: string | null;
};

export type NotificationListResponse = {
  items: NotificationItem[];
  nextBeforeSequence: number | null;
  unreadCount: number;
  watermark: number;
};

export type NotificationReplayResponse = {
  items: NotificationItem[];
  nextCursor: string | null;
  watermark: number;
};

/** Redis-only envelope. It is reduced to NotificationItem before SSE output. */
export type RoutedNotificationMessage = NotificationItem & {
  userId: string;
  traceParent?: string | null;
  traceState?: string | null;
};
