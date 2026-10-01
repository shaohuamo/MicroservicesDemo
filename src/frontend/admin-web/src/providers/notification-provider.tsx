"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { SpanKind, SpanStatusCode, context, propagation, trace } from "@opentelemetry/api";
import {
  acknowledgeNotification,
  getNotifications,
  markAllNotificationsAsRead,
  markNotificationAsRead,
} from "@/lib/api/notifications";
import { normalizeNotificationItem } from "@/lib/notifications/notification-message";
import { retryNotificationAcknowledgement } from "@/lib/notifications/ack-retry";
import { NotificationToastViewport } from "@/components/notifications/notification-toast-viewport";
import type { NotificationItem } from "@/types/notification";

type RealtimeStatus = "disconnected" | "connecting" | "connected" | "reconnecting";

type NotificationContextValue = {
  notifications: NotificationItem[];
  unreadCount: number;
  watermark: number;
  realtimeStatus: RealtimeStatus;
  isLoading: boolean;
  isLoadingMore: boolean;
  isMarkingAllRead: boolean;
  readingNotificationIds: ReadonlySet<string>;
  hasMore: boolean;
  loadError: boolean;
  operationError: boolean;
  loadMore: () => Promise<void>;
  reload: () => void;
  markRead: (notificationId: string) => Promise<void>;
  markAllRead: () => Promise<void>;
};

const NotificationContext = createContext<NotificationContextValue | null>(null);
const RETRY_DELAY_MS = 5_000;

function mergeNotifications(
  existing: NotificationItem[],
  incoming: NotificationItem[],
) {
  const byId = new Map(incoming.map((item) => [item.notificationId, item]));
  for (const item of existing) {
    // Existing UI state wins, particularly an optimistic/local ReadAtUtc.
    byId.set(item.notificationId, item);
  }

  return [...byId.values()].sort(
    (left, right) => right.sequenceNumber - left.sequenceNumber,
  );
}

export function NotificationProvider({
  enabled,
  userId,
  children,
}: {
  enabled: boolean;
  userId: string | null;
  children: ReactNode;
}) {
  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [watermark, setWatermark] = useState(0);
  const [nextBeforeSequence, setNextBeforeSequence] = useState<number | null>(null);
  const [isLoading, setIsLoading] = useState(enabled);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [isMarkingAllRead, setIsMarkingAllRead] = useState(false);
  const [readingNotificationIds, setReadingNotificationIds] = useState<Set<string>>(new Set());
  const [loadError, setLoadError] = useState(false);
  const [operationError, setOperationError] = useState(false);
  const [reloadVersion, setReloadVersion] = useState(0);
  const [realtimeStatus, setRealtimeStatus] = useState<RealtimeStatus>(
    enabled ? "connecting" : "disconnected",
  );
  const [toastQueue, setToastQueue] = useState<NotificationItem[]>([]);
  const knownNotificationIdsRef = useRef(new Set<string>());
  const realtimeIdsDuringHistoryRef = useRef(new Set<string>());
  const historyRequestActiveRef = useRef(false);
  const ackTasksRef = useRef(new Map<string, Promise<void>>());
  const ackAbortRef = useRef<AbortController | null>(null);

  useEffect(() => {
    if (!enabled || !userId) return;
    const controller = new AbortController();
    const ackTasks = ackTasksRef.current;
    ackAbortRef.current = controller;
    return () => {
      controller.abort();
      if (ackAbortRef.current === controller) ackAbortRef.current = null;
      ackTasks.clear();
      ackTasksRef.current = new Map();
    };
  }, [enabled, userId]);

  useEffect(() => {
    if (!enabled || !userId) {
      return;
    }

    const abortController = new AbortController();
    let retryTimer: ReturnType<typeof setTimeout> | null = null;

    void (async () => {
      // Yield once so all effect-driven state changes originate from an async
      // synchronization callback rather than causing a cascading render.
      await Promise.resolve();
      if (abortController.signal.aborted) return;
      setIsLoading(true);
      setLoadError(false);
      historyRequestActiveRef.current = true;
      realtimeIdsDuringHistoryRef.current.clear();

      try {
        const response = await getNotifications({
          limit: 20,
          signal: abortController.signal,
        });
        if (abortController.signal.aborted) return;
        const responseIds = new Set(
          response.items.map((item) => item.notificationId),
        );
        for (const notificationId of responseIds) {
          knownNotificationIdsRef.current.add(notificationId);
        }
        const realtimeUnreadOutsideSnapshot = [
          ...realtimeIdsDuringHistoryRef.current,
        ].filter((notificationId) => !responseIds.has(notificationId)).length;

        setNotifications((current) => mergeNotifications(current, response.items));
        setUnreadCount(response.unreadCount + realtimeUnreadOutsideSnapshot);
        setWatermark((current) => Math.max(current, response.watermark));
        setNextBeforeSequence(response.nextBeforeSequence);
      } catch {
        if (!abortController.signal.aborted) {
          setLoadError(true);
          retryTimer = setTimeout(() => {
            setReloadVersion((current) => current + 1);
          }, RETRY_DELAY_MS);
        }
      } finally {
        if (!abortController.signal.aborted) {
          historyRequestActiveRef.current = false;
          setIsLoading(false);
        }
      }
    })();

    return () => {
      abortController.abort();
      if (retryTimer) clearTimeout(retryTimer);
      historyRequestActiveRef.current = false;
    };
  }, [enabled, userId, reloadVersion]);

  const queueAcknowledgement = useCallback((notification: NotificationItem) => {
    if (!userId || ackTasksRef.current.has(notification.notificationId)) return;
    const signal = ackAbortRef.current?.signal;
    if (!signal) return;
    const tasks = ackTasksRef.current;
    const task = retryNotificationAcknowledgement(
      notification.notificationId,
      acknowledgeNotification,
      signal,
    ).then(() => undefined).finally(() => { tasks.delete(notification.notificationId); });
    tasks.set(notification.notificationId, task);
  }, [userId]);

  useEffect(() => {
    if (!enabled || !userId) return;

    let eventSource: EventSource | null = null;
    let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
    let disposed = false;
    const onNotification = (event: Event) => {
      let parsed: unknown;
      try {
        parsed = JSON.parse((event as MessageEvent<string>).data);
      } catch {
        return;
      }

      const notification = normalizeNotificationItem(parsed);
      if (!notification) return;

      const raw = parsed && typeof parsed === "object"
        ? parsed as Record<string, unknown>
        : {};
      const traceParent = typeof raw.traceparent === "string" ? raw.traceparent : undefined;
      const traceState = typeof raw.tracestate === "string" ? raw.tracestate : undefined;
      const remoteContext = propagation.extract(context.active(), {
        traceparent: traceParent,
        tracestate: traceState,
      });
      const receiveSpan = trace.getTracer("admin-web.notifications").startSpan(
        "notifications.sse.receive",
        { kind: SpanKind.CONSUMER },
        remoteContext,
      );
      receiveSpan.setAttribute("notification.id", notification.notificationId);

      try {
        const isKnown = knownNotificationIdsRef.current.has(
          notification.notificationId,
        );

        if (!isKnown) {
          knownNotificationIdsRef.current.add(notification.notificationId);
          if (historyRequestActiveRef.current && !notification.readAtUtc) {
            realtimeIdsDuringHistoryRef.current.add(notification.notificationId);
          }
          setNotifications((current) => mergeNotifications(current, [notification]));
          setWatermark((current) => Math.max(current, notification.sequenceNumber));
          if (!notification.readAtUtc) {
            setUnreadCount((current) => current + 1);
          }
          setToastQueue((current) => [...current, notification]);
        }

        // ACK duplicates as well: a previous ACK may have failed before
        // NotificationsMicroservice committed it.
        queueAcknowledgement(notification);
        receiveSpan.setStatus({ code: SpanStatusCode.OK });
      } catch (error) {
        receiveSpan.recordException(error as Error);
        receiveSpan.setStatus({ code: SpanStatusCode.ERROR });
      } finally {
        receiveSpan.end();
      }
    };

    const connect = () => {
      if (disposed) return;
      const streamUrl = new URL("/api/notifications/stream", window.location.origin);

      const source = new EventSource(streamUrl.toString());
      eventSource = source;
      queueMicrotask(() => {
        if (!disposed && eventSource === source) setRealtimeStatus("connecting");
      });

      source.onopen = () => {
        if (disposed || eventSource !== source) return;
        if (reconnectTimer) clearTimeout(reconnectTimer);
        reconnectTimer = null;
        setRealtimeStatus("connected");
      };

      source.onerror = () => {
        if (disposed || eventSource !== source) return;
        setRealtimeStatus("reconnecting");
        // EventSource retries dropped streams itself, but a rejected HTTP
        // response (such as a temporary 503) can leave it permanently closed.
        if (source.readyState !== EventSource.CLOSED || reconnectTimer) return;
        reconnectTimer = setTimeout(() => {
          reconnectTimer = null;
          if (disposed || eventSource !== source) return;
          source.close();
          connect();
        }, RETRY_DELAY_MS);
      };

      source.addEventListener("server-error", () => {
        if (!disposed && eventSource === source) setRealtimeStatus("reconnecting");
      });
      source.addEventListener("notification", (event) => {
        if (!disposed && eventSource === source) onNotification(event);
      });
    };

    connect();

    return () => {
      disposed = true;
      if (reconnectTimer) clearTimeout(reconnectTimer);
      eventSource?.close();
      setRealtimeStatus("disconnected");
    };
  }, [enabled, userId, queueAcknowledgement]);

  const loadMore = useCallback(async () => {
    if (isLoadingMore || nextBeforeSequence === null) return;
    setIsLoadingMore(true);
    setOperationError(false);

    try {
      const response = await getNotifications({
        beforeSequence: nextBeforeSequence,
        limit: 20,
      });
      for (const item of response.items) {
        knownNotificationIdsRef.current.add(item.notificationId);
      }
      setNotifications((current) => mergeNotifications(current, response.items));
      setNextBeforeSequence(response.nextBeforeSequence);
      setWatermark((current) => Math.max(current, response.watermark));
    } catch {
      setOperationError(true);
    } finally {
      setIsLoadingMore(false);
    }
  }, [isLoadingMore, nextBeforeSequence]);

  const markRead = useCallback(async (notificationId: string) => {
    const target = notifications.find(
      (notification) => notification.notificationId === notificationId,
    );
    if (!target || target.readAtUtc || readingNotificationIds.has(notificationId)) {
      return;
    }

    setReadingNotificationIds((current) => new Set(current).add(notificationId));
    setOperationError(false);

    try {
      await markNotificationAsRead(notificationId);
      const readAtUtc = new Date().toISOString();
      setNotifications((current) => current.map((notification) =>
        notification.notificationId === notificationId
          ? { ...notification, readAtUtc }
          : notification
      ));
      setUnreadCount((current) => Math.max(0, current - 1));
    } catch {
      setOperationError(true);
    } finally {
      setReadingNotificationIds((current) => {
        const next = new Set(current);
        next.delete(notificationId);
        return next;
      });
    }
  }, [notifications, readingNotificationIds]);

  const markAllRead = useCallback(async () => {
    if (isMarkingAllRead || unreadCount === 0) return;
    const upToSequence = Math.max(
      watermark,
      ...notifications.map((notification) => notification.sequenceNumber),
    );
    if (upToSequence <= 0) return;

    setIsMarkingAllRead(true);
    setOperationError(false);

    try {
      await markAllNotificationsAsRead(upToSequence);
      const readAtUtc = new Date().toISOString();
      setNotifications((current) => current.map((notification) =>
        !notification.readAtUtc && notification.sequenceNumber <= upToSequence
          ? { ...notification, readAtUtc }
          : notification
      ));
      // Preserve notifications arriving over SSE while this request was in
      // flight by subtracting only the unread count captured at submission.
      setUnreadCount((current) => Math.max(0, current - unreadCount));
    } catch {
      setOperationError(true);
    } finally {
      setIsMarkingAllRead(false);
    }
  }, [isMarkingAllRead, notifications, unreadCount, watermark]);

  const dismissToast = useCallback((notificationId: string) => {
    setToastQueue((current) => current.filter(
      (notification) => notification.notificationId !== notificationId,
    ));
  }, []);

  const value = useMemo<NotificationContextValue>(() => ({
    notifications,
    unreadCount,
    watermark,
    realtimeStatus,
    isLoading,
    isLoadingMore,
    isMarkingAllRead,
    readingNotificationIds,
    hasMore: nextBeforeSequence !== null,
    loadError,
    operationError,
    loadMore,
    reload: () => setReloadVersion((current) => current + 1),
    markRead,
    markAllRead,
  }), [
    notifications,
    unreadCount,
    watermark,
    realtimeStatus,
    isLoading,
    isLoadingMore,
    isMarkingAllRead,
    readingNotificationIds,
    nextBeforeSequence,
    loadError,
    operationError,
    loadMore,
    markRead,
    markAllRead,
  ]);

  return (
    <NotificationContext.Provider value={value}>
      {children}
      <NotificationToastViewport
        notifications={toastQueue}
        onDismiss={dismissToast}
      />
    </NotificationContext.Provider>
  );
}

export function useNotifications() {
  const context = useContext(NotificationContext);
  if (!context) {
    throw new Error("useNotifications must be used inside NotificationProvider.");
  }

  return context;
}
