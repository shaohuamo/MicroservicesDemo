"use client";

import { useCallback, useEffect, useRef } from "react";
import { useI18n } from "@/lib/i18n/provider";
import {
  getNotificationDetail,
  getNotificationTitle,
} from "@/lib/notifications/presentation";
import type { NotificationItem } from "@/types/notification";

type NotificationToastViewportProps = {
  notifications: NotificationItem[];
  onDismiss: (notificationId: string) => void;
};

function NotificationToast({
  notification,
  onDismiss,
}: {
  notification: NotificationItem;
  onDismiss: () => void;
}) {
  const { t } = useI18n();
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const startedAtRef = useRef(0);
  const remainingRef = useRef(notification.status === "Success" ? 6_000 : 10_000);

  const pause = useCallback(() => {
    if (!timeoutRef.current) return;
    clearTimeout(timeoutRef.current);
    timeoutRef.current = null;
    remainingRef.current = Math.max(
      0,
      remainingRef.current - (Date.now() - startedAtRef.current),
    );
  }, []);

  const resume = useCallback(() => {
    if (timeoutRef.current) return;
    startedAtRef.current = Date.now();
    timeoutRef.current = setTimeout(onDismiss, remainingRef.current);
  }, [onDismiss]);

  useEffect(() => {
    resume();
    return pause;
  }, [pause, resume]);

  const succeeded = notification.status === "Success";

  return (
    <article
      role={succeeded ? "status" : "alert"}
      aria-live={succeeded ? "polite" : "assertive"}
      onMouseEnter={pause}
      onMouseLeave={resume}
      onFocus={pause}
      onBlur={resume}
      className="pointer-events-auto overflow-hidden rounded-[1.15rem] border border-[var(--border-strong)] bg-white shadow-[0_20px_48px_rgba(41,90,160,0.2)]"
    >
      <div className="flex gap-3 p-4">
        <span
          className={`mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-full ${
            succeeded
              ? "bg-emerald-50 text-emerald-600"
              : "bg-red-50 text-red-500"
          }`}
          aria-hidden="true"
        >
          {succeeded ? (
            <svg viewBox="0 0 24 24" fill="none" className="h-5 w-5">
              <path d="m5 12 4 4L19 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          ) : (
            <svg viewBox="0 0 24 24" fill="none" className="h-5 w-5">
              <path d="M12 8v5m0 3.5v.01M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
            </svg>
          )}
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-base font-bold text-[var(--text)]">
            {getNotificationTitle(notification, t)}
          </p>
          <p className="mt-1 text-base leading-6 text-[var(--muted)]">
            {getNotificationDetail(notification, t)}
          </p>
        </div>
        <button
          type="button"
          aria-label={t("notifications.dismiss")}
          onClick={onDismiss}
          className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-[var(--muted)] hover:bg-[var(--surface)] hover:text-[var(--text)]"
        >
          <svg viewBox="0 0 24 24" fill="none" className="h-4 w-4" aria-hidden="true">
            <path d="m7 7 10 10M17 7 7 17" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
          </svg>
        </button>
      </div>
      <div className={`h-1 ${succeeded ? "bg-emerald-400" : "bg-red-400"}`} />
    </article>
  );
}

export function NotificationToastViewport({
  notifications,
  onDismiss,
}: NotificationToastViewportProps) {
  return (
    <div
      className="pointer-events-none fixed right-4 top-4 z-[90] flex w-[min(24rem,calc(100vw-2rem))] flex-col gap-3"
      aria-label="Notifications"
    >
      {notifications.slice(0, 3).map((notification) => (
        <NotificationToast
          key={notification.notificationId}
          notification={notification}
          onDismiss={() => onDismiss(notification.notificationId)}
        />
      ))}
    </div>
  );
}
