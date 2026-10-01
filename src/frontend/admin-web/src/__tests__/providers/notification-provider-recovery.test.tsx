import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import { getNotifications } from "@/lib/api/notifications";
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

describe("notification connection recovery", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal("EventSource", FakeEventSource);
    sources.length = 0;
    vi.mocked(getNotifications).mockReset();
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
});
