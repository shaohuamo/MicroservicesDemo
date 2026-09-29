import type {
  NotificationDeliveryStatus,
  NotificationItem,
  ProductOperation,
  ProductOperationStatus,
  RoutedNotificationMessage,
} from "@/types/notification";

function asRecord(value: unknown): Record<string, unknown> | null {
  return value && typeof value === "object" && !Array.isArray(value)
    ? value as Record<string, unknown>
    : null;
}

function read(record: Record<string, unknown>, camel: string, pascal: string) {
  return record[camel] ?? record[pascal];
}

function optionalString(value: unknown) {
  return typeof value === "string" && value.length > 0 ? value : null;
}

function normalizeOperation(value: unknown): ProductOperation | null {
  if (typeof value !== "string") return null;

  switch (value.toLowerCase()) {
    case "add":
      return "Add";
    case "update":
      return "Update";
    case "delete":
      return "Delete";
    default:
      return null;
  }
}

function normalizeStatus(value: unknown): ProductOperationStatus | null {
  if (typeof value !== "string") return null;

  switch (value.toLowerCase()) {
    case "success":
      return "Success";
    case "failure":
    case "failed":
      return "Failure";
    default:
      return null;
  }
}

const DELIVERY_STATUSES = new Set<NotificationDeliveryStatus>([
  "Pending",
  "AwaitingSseAck",
  "RetryScheduled",
  "SendingEmail",
  "DeliveredInApp",
  "DeliveredEmail",
  "Failed",
]);

function normalizeDeliveryStatus(value: unknown) {
  if (typeof value !== "string") return undefined;

  const status = [...DELIVERY_STATUSES].find(
    (candidate) => candidate.toLowerCase() === value.toLowerCase(),
  );

  return status;
}

export function normalizeNotificationItem(value: unknown): NotificationItem | null {
  const record = asRecord(value);
  if (!record) return null;

  const notificationId = read(record, "notificationId", "NotificationId");
  const rawSequenceNumber = read(record, "sequenceNumber", "SequenceNumber");
  const sequenceNumber = typeof rawSequenceNumber === "number"
    ? rawSequenceNumber
    : Number(rawSequenceNumber);
  const operation = normalizeOperation(read(record, "operation", "Operation"));
  const status = normalizeStatus(read(record, "status", "Status"));
  const occurredAtUtc = read(record, "occurredAtUtc", "OccurredAtUtc");

  if (
    typeof notificationId !== "string"
    || notificationId.length === 0
    || !Number.isSafeInteger(sequenceNumber)
    || sequenceNumber < 0
    || !operation
    || !status
    || typeof occurredAtUtc !== "string"
    || occurredAtUtc.length === 0
  ) {
    return null;
  }

  const deliveryStatus = normalizeDeliveryStatus(
    read(record, "deliveryStatus", "DeliveryStatus"),
  );
  const deliveredAtUtc = optionalString(
    read(record, "deliveredAtUtc", "DeliveredAtUtc"),
  );
  const readAtUtc = optionalString(read(record, "readAtUtc", "ReadAtUtc"));

  return {
    notificationId,
    sequenceNumber,
    operation,
    status,
    productId: optionalString(read(record, "productId", "ProductId")),
    productName: optionalString(read(record, "productName", "ProductName")),
    occurredAtUtc,
    errorCode: optionalString(read(record, "errorCode", "ErrorCode")),
    ...(deliveryStatus ? { deliveryStatus } : {}),
    ...(deliveredAtUtc ? { deliveredAtUtc } : {}),
    readAtUtc,
  };
}

export function normalizeRoutedNotification(
  value: unknown,
): RoutedNotificationMessage | null {
  const record = asRecord(value);
  const notification = normalizeNotificationItem(value);
  const userId = record ? read(record, "userId", "UserId") : null;
  const traceParent = record
    ? optionalString(read(record, "traceParent", "TraceParent"))
    : null;
  const traceState = record
    ? optionalString(read(record, "traceState", "TraceState"))
    : null;

  if (!notification || typeof userId !== "string" || userId.length === 0) {
    return null;
  }

  return {
    ...notification,
    userId,
    ...(traceParent ? { traceParent } : {}),
    ...(traceState ? { traceState } : {}),
  };
}
