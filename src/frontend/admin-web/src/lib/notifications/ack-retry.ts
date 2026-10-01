function waitForRetry(delayMs: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve) => {
    if (signal.aborted) return resolve();
    const onAbort = () => {
      clearTimeout(timer);
      resolve();
    };
    const timer = setTimeout(() => {
      signal.removeEventListener("abort", onAbort);
      resolve();
    }, delayMs);
    signal.addEventListener("abort", onAbort, { once: true });
  });
}

export async function retryNotificationAcknowledgement(
  notificationId: string,
  acknowledge: (notificationId: string) => Promise<void>,
  signal: AbortSignal,
): Promise<boolean> {
  for (let attempt = 0; attempt < 5 && !signal.aborted; attempt++) {
    try {
      await acknowledge(notificationId);
      return !signal.aborted;
    } catch {
      if (attempt === 4 || signal.aborted) return false;
      await waitForRetry(Math.min(1_000 * 2 ** attempt, 8_000), signal);
    }
  }
  return false;
}
