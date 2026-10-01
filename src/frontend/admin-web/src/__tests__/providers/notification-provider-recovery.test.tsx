import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import { acknowledgeNotification, getNotifications } from "@/lib/api/notifications";
import { NotificationProvider, useNotifications } from "@/providers/notification-provider";

vi.mock("@/lib/api/notifications", () => ({
  getNotifications: vi.fn(),
  acknowledgeNotification: vi.fn(),
  markAllNotificationsAsRead: vi.fn(),
  markNotificationAsRead: vi.fn(),
}));
vi.mock("@/components/notifications/notification-toast-viewport", () => ({
  NotificationToastViewport: () => null,
}));

const sources: FakeEventSource[] = [];

class FakeEventSource {
  static readonly CLOSED = 2;
  readyState = 0;
  onopen: ((event: Event) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  close = vi.fn(() => { this.readyState = FakeEventSource.CLOSED; });
  addEventListener = vi.fn();

  constructor(readonly url: string) {
    sources.push(this);
  }
}

function Status() {
  const { loadError, realtimeStatus } = useNotifications();
  return <div>{`${loadError}:${realtimeStatus}`}</div>;
}

function ListState() {
  const { notifications, unreadCount } = useNotifications();
  return <div>{`${notifications.map((item) => item.sequenceNumber).join(",")}:${unreadCount}`}</div>;
}

describe("notification connection recovery", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal("EventSource", FakeEventSource);
    sources.length = 0;
    vi.mocked(getNotifications).mockReset();
    vi.mocked(acknowledgeNotification).mockReset();
    vi.mocked(acknowledgeNotification).mockResolvedValue(undefined);
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it("retries failed history and a permanently closed SSE connection", async () => {
    vi.mocked(getNotifications)
      .mockRejectedValueOnce(new Error("Temporary authentication failure"))
      .mockResolvedValue({
        items: [],
        nextBeforeSequence: null,
        unreadCount: 0,
        watermark: 0,
      });

    render(
      <NotificationProvider enabled userId="user-1">
        <Status />
      </NotificationProvider>,
    );

    await act(async () => { await Promise.resolve(); });
    expect(getNotifications).toHaveBeenCalledTimes(1);
    expect(sources).toHaveLength(1);
    expect(new URL(sources[0].url).searchParams.has("afterSequence")).toBe(false);
    expect(screen.getByText("true:connecting")).toBeInTheDocument();

    sources[0].readyState = FakeEventSource.CLOSED;
    act(() => { sources[0].onerror?.(new Event("error")); });

    await act(async () => { await vi.advanceTimersByTimeAsync(5_000); });
    expect(getNotifications).toHaveBeenCalledTimes(2);
    expect(sources).toHaveLength(2);
    expect(sources[0].close).toHaveBeenCalledOnce();

    act(() => { sources[1].onopen?.(new Event("open")); });
    expect(screen.getByText("false:connected")).toBeInTheDocument();
  });

  it("lets EventSource reconnect a dropped stream without creating a second one", async () => {
    vi.mocked(getNotifications).mockResolvedValue({
      items: [],
      nextBeforeSequence: null,
      unreadCount: 0,
      watermark: 0,
    });

    render(
      <NotificationProvider enabled userId="user-1">
        <Status />
      </NotificationProvider>,
    );

    await act(async () => { await Promise.resolve(); });
    act(() => { sources[0].onerror?.(new Event("error")); });
    await act(async () => { await vi.advanceTimersByTimeAsync(5_000); });

    expect(sources).toHaveLength(1);
    act(() => { sources[0].onopen?.(new Event("open")); });
    expect(screen.getByText("false:connected")).toBeInTheDocument();
  });

  it("keeps the list descending and counts duplicates once when SSE replay arrives oldest first", async () => {
    vi.mocked(getNotifications).mockResolvedValue({
      items: [], nextBeforeSequence: null, unreadCount: 0, watermark: 0,
    });
    render(
      <NotificationProvider enabled userId="user-1">
        <ListState />
      </NotificationProvider>,
    );
    await act(async () => { await Promise.resolve(); });
    const receive = sources[0].addEventListener.mock.calls.find(([name]) => name === "notification")?.[1] as (event: MessageEvent<string>) => void;
    const oldest = {
      notificationId: "oldest", sequenceNumber: 100, operation: "Add", status: "Success",
      productId: null, productName: null, occurredAtUtc: "2026-01-01T00:00:00Z", errorCode: null,
    };
    const newest = { ...oldest, notificationId: "newest", sequenceNumber: 50, occurredAtUtc: "2026-01-02T00:00:00Z" };

    await act(async () => {
      receive(new MessageEvent("notification", { data: JSON.stringify(oldest) }));
      receive(new MessageEvent("notification", { data: JSON.stringify(newest) }));
    });
    expect(screen.getByText("100,50:2")).toBeInTheDocument();

    await act(async () => {
      receive(new MessageEvent("notification", { data: JSON.stringify(oldest) }));
    });
    expect(screen.getByText("100,50:2")).toBeInTheDocument();
    expect(acknowledgeNotification).toHaveBeenCalledWith("oldest");
    expect(acknowledgeNotification).toHaveBeenCalledWith("newest");
  });
});
