import { afterEach, describe, expect, it, vi } from "vitest";
import { retryNotificationAcknowledgement } from "@/lib/notifications/ack-retry";

describe("notification acknowledgement retry", () => {
  afterEach(() => vi.useRealTimers());

  it("retries a lost ACK and succeeds without changing the notification ID", async () => {
    vi.useFakeTimers();
    const acknowledge = vi.fn()
      .mockRejectedValueOnce(new Error("network unavailable"))
      .mockRejectedValueOnce(new Error("response lost"))
      .mockResolvedValueOnce(undefined);
    const controller = new AbortController();

    const result = retryNotificationAcknowledgement("notification-1", acknowledge, controller.signal);
    await vi.advanceTimersByTimeAsync(3_000);

    expect(await result).toBe(true);
    expect(acknowledge).toHaveBeenCalledTimes(3);
    expect(acknowledge).toHaveBeenNthCalledWith(3, "notification-1");
  });

  it("stops retrying when the SSE owner is disposed", async () => {
    vi.useFakeTimers();
    const acknowledge = vi.fn().mockRejectedValue(new Error("network unavailable"));
    const controller = new AbortController();

    const result = retryNotificationAcknowledgement("notification-2", acknowledge, controller.signal);
    await vi.advanceTimersByTimeAsync(100);
    controller.abort();
    await vi.runAllTimersAsync();

    expect(await result).toBe(false);
    expect(acknowledge).toHaveBeenCalledOnce();
  });
});
