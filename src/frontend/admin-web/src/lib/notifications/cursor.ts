const CURSOR_STORAGE_PREFIX = "notifications:last-acknowledged-sequence:";

function getCursorStorageKey(userId: string) {
  return `${CURSOR_STORAGE_PREFIX}${encodeURIComponent(userId)}`;
}

export function readStoredCursor(userId: string): number {
  if (!userId) return 0;

  try {
    const value = sessionStorage.getItem(getCursorStorageKey(userId));
    if (!value || !/^\d+$/.test(value)) return 0;
    const parsed = Number(value);
    return Number.isSafeInteger(parsed) ? parsed : 0;
  } catch {
    return 0;
  }
}

export function persistAcknowledgedCursor(userId: string, sequenceNumber: number) {
  if (!userId || !Number.isSafeInteger(sequenceNumber) || sequenceNumber < 0) return;

  try {
    const current = readStoredCursor(userId);
    if (sequenceNumber > current) {
      sessionStorage.setItem(getCursorStorageKey(userId), String(sequenceNumber));
    }
  } catch {
    // Session storage can be disabled. Native EventSource Last-Event-ID still
    // covers transport reconnects for the current page.
  }
}
