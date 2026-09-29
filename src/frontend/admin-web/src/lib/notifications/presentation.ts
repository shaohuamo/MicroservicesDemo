import type { TranslationKey } from "@/lib/i18n/dictionaries";
import type { NotificationItem } from "@/types/notification";

type Translate = (
  key: TranslationKey,
  values?: Record<string, string | number>,
) => string;

export function getNotificationTitle(
  notification: NotificationItem,
  t: Translate,
) {
  const keys: Record<
    NotificationItem["operation"],
    Record<NotificationItem["status"], TranslationKey>
  > = {
    Add: {
      Success: "notifications.title.add.success",
      Failure: "notifications.title.add.failure",
    },
    Update: {
      Success: "notifications.title.update.success",
      Failure: "notifications.title.update.failure",
    },
    Delete: {
      Success: "notifications.title.delete.success",
      Failure: "notifications.title.delete.failure",
    },
  };

  return t(keys[notification.operation][notification.status]);
}

function getErrorMessageKey(errorCode: string | null): TranslationKey {
  switch (errorCode?.toLowerCase()) {
    case "productnotfound":
    case "product_not_found":
    case "product.update.not_found":
    case "product.delete.not_found":
    case "notfound":
      return "notifications.error.notFound";
    case "validationfailed":
    case "validation_failed":
    case "validation":
      return "notifications.error.validation";
    case "conflict":
    case "productconflict":
    case "product_conflict":
      return "notifications.error.conflict";
    default:
      return "notifications.error.generic";
  }
}

export function getNotificationDetail(
  notification: NotificationItem,
  t: Translate,
) {
  const productName = notification.productName || t("notifications.unknownProduct");

  if (notification.status === "Failure") {
    return t(getErrorMessageKey(notification.errorCode), { productName });
  }

  return t("notifications.productDetail", { productName });
}
