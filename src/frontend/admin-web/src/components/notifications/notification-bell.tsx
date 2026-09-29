"use client";

import { useEffect, useRef, useState } from "react";
import { useI18n } from "@/lib/i18n/provider";
import {
  getNotificationDetail,
  getNotificationTitle,
} from "@/lib/notifications/presentation";
import { useNotifications } from "@/providers/notification-provider";
import type { NotificationItem } from "@/types/notification";

function formatNotificationTime(value: string, locale: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;

  return new Intl.DateTimeFormat(locale, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(date);
}

function StatusIcon({ succeeded }: { succeeded: boolean }) {
  return (
    <span
      className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full ${
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
  );
}

function NotificationRow({ notification }: { notification: NotificationItem }) {
  const { locale, t } = useI18n();
  const { markRead, readingNotificationIds } = useNotifications();
  const unread = !notification.readAtUtc;
  const isReading = readingNotificationIds.has(notification.notificationId);

  return (
    <li className={unread ? "bg-[var(--accent-soft)]/55" : "bg-white"}>
      <button
        type="button"
        disabled={isReading}
        onClick={() => void markRead(notification.notificationId)}
        className="flex w-full gap-3 px-4 py-4 text-left hover:bg-[var(--surface)] disabled:cursor-wait disabled:opacity-70"
        aria-label={
          unread
            ? `${getNotificationTitle(notification, t)}. ${t("notifications.markRead")}`
            : getNotificationTitle(notification, t)
        }
      >
        <StatusIcon succeeded={notification.status === "Success"} />
        <span className="min-w-0 flex-1">
          <span className="flex items-start gap-2">
            <span className="min-w-0 flex-1 text-base font-bold text-[var(--text)]">
              {getNotificationTitle(notification, t)}
            </span>
            {unread && (
              <span
                className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-[var(--accent)]"
                aria-label={t("notifications.unread")}
              />
            )}
          </span>
          <span className="mt-1 block text-base leading-6 text-[var(--muted)]">
            {getNotificationDetail(notification, t)}
          </span>
          <span className="mt-2 flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-[var(--muted)]">
            <time dateTime={notification.occurredAtUtc}>
              {formatNotificationTime(notification.occurredAtUtc, locale)}
            </time>
            {notification.deliveryStatus === "DeliveredEmail" && (
              <>
                <span aria-hidden="true">·</span>
                <span>{t("notifications.deliveredByEmail")}</span>
              </>
            )}
            {isReading && <span>{t("notifications.markingRead")}</span>}
          </span>
        </span>
      </button>
    </li>
  );
}

export function NotificationBell() {
  const { t } = useI18n();
  const {
    notifications,
    unreadCount,
    realtimeStatus,
    isLoading,
    isLoadingMore,
    isMarkingAllRead,
    hasMore,
    loadError,
    operationError,
    loadMore,
    reload,
    markAllRead,
  } = useNotifications();
  const [isOpen, setIsOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!isOpen) return;

    function handlePointerDown(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setIsOpen(false);
    }

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen]);

  return (
    <div className="relative" ref={rootRef}>
      <button
        type="button"
        aria-expanded={isOpen}
        aria-haspopup="dialog"
        aria-label={t("notifications.open", { count: unreadCount })}
        onClick={() => setIsOpen((current) => !current)}
        className="relative flex h-[3.15rem] w-[3.15rem] items-center justify-center rounded-full border border-[var(--border-strong)] bg-white text-[var(--text)] shadow-[0_12px_28px_rgba(41,90,160,0.08)] hover:bg-[var(--surface)]"
      >
        <svg viewBox="0 0 24 24" fill="none" className="h-5 w-5" aria-hidden="true">
          <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9ZM10 21h4" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        {unreadCount > 0 && (
          <span className="absolute -right-1 -top-1 flex min-h-5 min-w-5 items-center justify-center rounded-full border-2 border-white bg-red-500 px-1 text-[0.65rem] font-bold leading-none text-white">
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <>
          <button
            type="button"
            aria-label={t("notifications.close")}
            onClick={() => setIsOpen(false)}
            className="fixed inset-0 z-40 bg-slate-900/20 backdrop-blur-[2px] sm:hidden"
          />
          <section
            role="dialog"
            aria-modal="true"
            aria-label={t("notifications.title")}
            className="fixed inset-x-3 bottom-3 top-20 z-50 flex flex-col overflow-hidden rounded-[1.4rem] border border-[var(--border-strong)] bg-white shadow-[0_24px_64px_rgba(41,90,160,0.24)] sm:absolute sm:inset-auto sm:right-0 sm:top-[calc(100%+0.65rem)] sm:h-[min(38rem,calc(100vh-7rem))] sm:w-[26rem]"
          >
            <header className="border-b border-[var(--border)] px-4 py-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <h2 className="font-display text-2xl font-semibold text-[var(--text)]">
                    {t("notifications.title")}
                  </h2>
                  <p className="mt-1 text-sm text-[var(--muted)]">
                    {t("notifications.unreadCount", { count: unreadCount })}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    disabled={unreadCount === 0 || isMarkingAllRead}
                    onClick={() => void markAllRead()}
                    className="rounded-full px-3 py-2 text-sm font-bold text-[var(--accent-strong)] hover:bg-[var(--accent-soft)] disabled:cursor-not-allowed disabled:opacity-45"
                  >
                    {isMarkingAllRead
                      ? t("notifications.markingAllRead")
                      : t("notifications.markAllRead")}
                  </button>
                  <button
                    type="button"
                    aria-label={t("notifications.close")}
                    onClick={() => setIsOpen(false)}
                    className="flex h-8 w-8 items-center justify-center rounded-full text-[var(--muted)] hover:bg-[var(--surface)] sm:hidden"
                  >
                    <svg viewBox="0 0 24 24" fill="none" className="h-4 w-4" aria-hidden="true">
                      <path d="m7 7 10 10M17 7 7 17" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
                    </svg>
                  </button>
                </div>
              </div>
              {realtimeStatus === "reconnecting" && (
                <p className="mt-3 flex items-center gap-2 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">
                  <span className="h-2 w-2 animate-pulse rounded-full bg-amber-500" aria-hidden="true" />
                  {t("notifications.reconnecting")}
                </p>
              )}
              {operationError && (
                <p role="alert" className="mt-3 text-sm text-red-600">
                  {t("notifications.actionFailed")}
                </p>
              )}
            </header>

            <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain">
              {isLoading && (
                <div className="space-y-3 p-4" aria-label={t("notifications.loading")}>
                  {[0, 1, 2].map((item) => (
                    <div key={item} className="skeleton-block h-20 rounded-[1rem]" />
                  ))}
                </div>
              )}

              {!isLoading && loadError && (
                <div className="flex h-full flex-col items-center justify-center p-8 text-center">
                  <p className="text-base font-semibold text-[var(--text)]">
                    {t("notifications.loadFailed")}
                  </p>
                  <button
                    type="button"
                    onClick={reload}
                    className="mt-4 rounded-full bg-[var(--accent)] px-4 py-2 text-sm font-bold text-white hover:bg-[var(--accent-strong)]"
                  >
                    {t("notifications.retry")}
                  </button>
                </div>
              )}

              {!isLoading && !loadError && notifications.length === 0 && (
                <div className="flex h-full flex-col items-center justify-center p-8 text-center">
                  <span className="flex h-14 w-14 items-center justify-center rounded-full bg-[var(--surface)] text-[var(--accent-strong)]" aria-hidden="true">
                    <svg viewBox="0 0 24 24" fill="none" className="h-6 w-6">
                      <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9ZM10 21h4" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </span>
                  <p className="mt-4 text-base font-bold text-[var(--text)]">
                    {t("notifications.emptyTitle")}
                  </p>
                  <p className="mt-2 text-base leading-6 text-[var(--muted)]">
                    {t("notifications.emptyDetail")}
                  </p>
                </div>
              )}

              {!isLoading && !loadError && notifications.length > 0 && (
                <ul className="divide-y divide-[var(--border)]">
                  {notifications.map((notification) => (
                    <NotificationRow
                      key={notification.notificationId}
                      notification={notification}
                    />
                  ))}
                </ul>
              )}
            </div>

            {hasMore && !loadError && (
              <footer className="border-t border-[var(--border)] p-3">
                <button
                  type="button"
                  disabled={isLoadingMore}
                  onClick={() => void loadMore()}
                  className="w-full rounded-full border border-[var(--border-strong)] bg-white px-4 py-2.5 text-base font-bold text-[var(--text)] hover:bg-[var(--surface)] disabled:cursor-wait disabled:opacity-60"
                >
                  {isLoadingMore
                    ? t("notifications.loadingMore")
                    : t("notifications.loadMore")}
                </button>
              </footer>
            )}
          </section>
        </>
      )}
    </div>
  );
}
