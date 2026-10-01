// @vitest-environment node

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { NextRequest } from "next/server";

const mocks = vi.hoisted(() => ({ auth: vi.fn(), registerConnection: vi.fn(), fetchReplayPage: vi.fn(), refreshAccessToken: vi.fn() }));
vi.mock("@/auth", () => ({ auth: mocks.auth }));
vi.mock("@/lib/auth/gateway-session-proof", () => ({ getRefreshTokenRecordId: vi.fn(async () => "11111111-1111-1111-1111-111111111111") }));
vi.mock("@/lib/auth/refresh-coordinator", () => ({ getOrRefreshAccessToken: mocks.refreshAccessToken }));
vi.mock("@/lib/notifications/server-hub", () => ({ registerNotificationConnection: mocks.registerConnection }));
vi.mock("@/lib/notifications/server-gateway", () => ({
  fetchNotificationReplayPage: mocks.fetchReplayPage,
  NotificationGatewayError: class NotificationGatewayError extends Error {
    constructor(message: string, public readonly status: number) { super(message); }
  },
}));
import { GET } from "@/app/api/notifications/stream/route";

const accessToken = "header." + Buffer.from(JSON.stringify({ exp: Math.floor(Date.now() / 1_000) + 3_600 })).toString("base64url") + ".signature";
const missedNotification = { notificationId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", sequenceNumber: 100, operation: "Add", status: "Success", productId: null, productName: null, occurredAtUtc: "2026-01-01T00:00:00Z", errorCode: null };
const emptyPage = { items: [], nextCursor: null, watermark: 0 };
function connection(available = () => true, generation = () => 0) {
  return {
    close: vi.fn(async () => undefined),
    refreshPresence: vi.fn(async () => undefined),
    isRealtimeAvailable: available,
    waitForRealtimeReady: vi.fn<(timeoutMs: number, signal: AbortSignal) => Promise<boolean>>(async () => available()),
    getDisconnectGeneration: generation,
    activateLiveDelivery: vi.fn(() => [] as typeof missedNotification[]),
  };
}
async function open(headers?: HeadersInit) {
  const response = await GET(new Request("http://localhost/api/notifications/stream?afterSequence=101", { headers }) as NextRequest);
  if (!response?.body) throw new Error("SSE response was not created.");
  return response;
}
const decode = (value: Uint8Array | undefined) => new TextDecoder().decode(value);

describe("notification stream unacknowledged replay and Redis fallback", () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout", "setInterval", "clearInterval"] });
    Object.values(mocks).forEach((mock) => mock.mockReset());
    mocks.auth.mockImplementation(async (handler?: (request: object) => Promise<Response>) => handler
      ? async () => handler({ auth: { userId: "user-1", accessToken } }) : { userId: "user-1" });
  });
  afterEach(() => { vi.useRealTimers(); });

  it("scans once on a healthy connection and ignores both browser receive cursors", async () => {
    const live = connection();
    mocks.registerConnection.mockResolvedValue(live);
    mocks.fetchReplayPage.mockResolvedValue({ items: [missedNotification], nextCursor: null, watermark: 101 });
    const response = await open({ "last-event-id": "101" });
    expect(response.status).toBe(200);
    const reader = response.body!.getReader();
    expect(decode((await reader.read()).value)).toContain(": connected");
    expect(decode((await reader.read()).value)).toContain("id: 100");
    await vi.advanceTimersByTimeAsync(15_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledOnce();
    expect(live.waitForRealtimeReady).toHaveBeenCalledWith(1_000, expect.any(AbortSignal));
    expect(mocks.fetchReplayPage.mock.calls[0][0]).not.toHaveProperty("afterSequence");
    await reader.cancel();
    expect(live.close).toHaveBeenCalledOnce();
  });

  it("waits for delayed presence before replay and does not schedule a second startup scan", async () => {
    let available = false;
    let finishWait!: (ready: boolean) => void;
    const live = connection(() => available);
    live.waitForRealtimeReady.mockImplementation(() => new Promise<boolean>((resolve) => { finishWait = resolve; }));
    mocks.registerConnection.mockResolvedValue(live);
    mocks.fetchReplayPage.mockResolvedValue(emptyPage);

    const pendingResponse = open();
    await vi.advanceTimersByTimeAsync(0);
    expect(live.waitForRealtimeReady).toHaveBeenCalledOnce();
    expect(mocks.fetchReplayPage).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(500);
    available = true;
    finishWait(true);
    const response = await pendingResponse;

    await vi.advanceTimersByTimeAsync(15_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledOnce();
    await response.body!.cancel();
  });

  it("opens SSE after the one-second wait times out and retains the recovery scan", async () => {
    let available = false;
    const live = connection(() => available);
    live.waitForRealtimeReady.mockImplementation((timeoutMs) =>
      new Promise<boolean>((resolve) => setTimeout(() => resolve(false), timeoutMs)));
    mocks.registerConnection.mockResolvedValue(live);
    mocks.fetchReplayPage.mockResolvedValue(emptyPage);

    const pendingResponse = open();
    await vi.advanceTimersByTimeAsync(0);
    await vi.advanceTimersByTimeAsync(999);
    expect(mocks.fetchReplayPage).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    const response = await pendingResponse;
    expect(response.status).toBe(200);
    expect(mocks.fetchReplayPage).toHaveBeenCalledOnce();

    available = true;
    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(2);
    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(2);
    await response.body!.cancel();
  });

  it("closes the local connection without replay if the request is cancelled during readiness wait", async () => {
    const controller = new AbortController();
    const live = connection(() => false);
    live.waitForRealtimeReady.mockImplementation((...args) => new Promise<boolean>((_resolve, reject) => {
      const signal = args[1];
      signal.addEventListener("abort", () => reject(signal.reason), { once: true });
    }));
    mocks.registerConnection.mockResolvedValue(live);
    const pendingResponse = GET(new Request("http://localhost/api/notifications/stream", {
      signal: controller.signal,
    }) as NextRequest);
    const rejected = expect(pendingResponse).rejects.toMatchObject({ name: "AbortError" });
    await vi.advanceTimersByTimeAsync(0);
    expect(live.waitForRealtimeReady).toHaveBeenCalledOnce();
    controller.abort();
    await rejected;

    expect(live.close).toHaveBeenCalledOnce();
    expect(mocks.fetchReplayPage).not.toHaveBeenCalled();
  });

  it("keeps recovery replay when Redis disconnects during the initial scan", async () => {
    let generation = 0;
    const live = connection(() => true, () => generation);
    mocks.registerConnection.mockResolvedValue(live);
    mocks.fetchReplayPage.mockImplementationOnce(async () => {
      generation++;
      return emptyPage;
    }).mockResolvedValue(emptyPage);
    const response = await open();

    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(2);
    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(2);
    await response.body!.cancel();
  });

  it("sends an earlier unacknowledged event and scans once more on recovery", async () => {
    let available = false;
    let generation = 0;
    mocks.registerConnection.mockResolvedValue(connection(() => available, () => generation));
    mocks.fetchReplayPage.mockResolvedValue({ items: [missedNotification], nextCursor: null, watermark: 101 });
    const response = await open({ "last-event-id": "101" });
    expect(mocks.fetchReplayPage).toHaveBeenCalledOnce();
    available = true;
    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(2);
    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(2);
    generation++;
    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(3);
    await response.body!.cancel();
  });

  it("starts fallback when Redis fails after SSE was established", async () => {
    let available = true;
    let generation = 0;
    mocks.registerConnection.mockResolvedValue(connection(() => available, () => generation));
    mocks.fetchReplayPage.mockResolvedValueOnce(emptyPage).mockResolvedValue({ items: [missedNotification], nextCursor: null, watermark: 101 });
    const response = await open();
    const reader = response.body!.getReader();
    await reader.read();
    available = false;
    generation++;
    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(2);
    expect(decode((await reader.read()).value)).toContain("id: 100");
    await reader.cancel();
  });

  it("runs a final scan when Redis recovers during an active fallback scan", async () => {
    let available = false;
    let finishScan!: (value: typeof emptyPage) => void;
    mocks.registerConnection.mockResolvedValue(connection(() => available));
    mocks.fetchReplayPage.mockResolvedValueOnce(emptyPage)
      .mockImplementationOnce(() => new Promise((resolve) => { finishScan = resolve; }))
      .mockResolvedValue(emptyPage);
    const response = await open();
    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(2);
    available = true;
    finishScan(emptyPage);
    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(3);
    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(3);
    await response.body!.cancel();
  });

  it("follows descending opaque cursors without dropping lower sequences or buffered messages", async () => {
    const newest = { ...missedNotification, sequenceNumber: 50, occurredAtUtc: "2026-01-02T00:00:00Z" };
    const older = { ...missedNotification, notificationId: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" };
    const buffered = { ...older, notificationId: "cccccccc-cccc-cccc-cccc-cccccccccccc", sequenceNumber: 10 };
    const live = connection();
    live.activateLiveDelivery.mockReturnValue([newest, buffered]);
    mocks.registerConnection.mockResolvedValue(live);
    mocks.fetchReplayPage.mockResolvedValueOnce({ items: [newest], nextCursor: "older-page", watermark: 200 })
      .mockResolvedValueOnce({ items: [older], nextCursor: null, watermark: 200 });
    const response = await open();
    const reader = response.body!.getReader();
    await reader.read();
    expect(decode((await reader.read()).value)).toContain("id: 50");
    expect(decode((await reader.read()).value)).toContain("id: 100");
    expect(decode((await reader.read()).value)).toContain("id: 10");
    expect(mocks.fetchReplayPage.mock.calls[1][0].cursor).toBe("older-page");
    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(2);
    await reader.cancel();
  });

  it("uses the same cursor protocol for a multi-page fallback", async () => {
    mocks.registerConnection.mockResolvedValue(connection(() => false));
    mocks.fetchReplayPage.mockResolvedValueOnce(emptyPage)
      .mockResolvedValueOnce({ items: [], nextCursor: "older-page", watermark: 200 })
      .mockResolvedValueOnce({ items: [missedNotification], nextCursor: null, watermark: 200 });
    const response = await open();
    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(3);
    expect(mocks.fetchReplayPage.mock.calls[2][0].cursor).toBe("older-page");
    await response.body!.cancel();
  });

  it("closes the stream if a replay cursor repeats", async () => {
    const live = connection();
    mocks.registerConnection.mockResolvedValue(live);
    mocks.fetchReplayPage.mockResolvedValue({ items: [], nextCursor: "repeated", watermark: 200 });
    const response = await open();
    await vi.waitFor(() => expect(live.close).toHaveBeenCalledOnce());
    expect(mocks.fetchReplayPage).toHaveBeenCalledTimes(2);
    await response.body!.cancel();
  });

  it("refreshes the access token for a long-lived SSE fallback", async () => {
    const expired = "header." + Buffer.from(JSON.stringify({ exp: 1 })).toString("base64url") + ".signature";
    mocks.auth.mockImplementation(async (handler?: (request: object) => Promise<Response>) => handler
      ? async () => handler({ auth: { userId: "user-1", accessToken: expired } }) : { userId: "user-1" });
    mocks.registerConnection.mockResolvedValue(connection(() => false));
    mocks.refreshAccessToken.mockResolvedValue({ status: "success", accessToken, expiresAt: Math.floor(Date.now() / 1_000) + 3_600 });
    mocks.fetchReplayPage.mockResolvedValue(emptyPage);
    const response = await open();
    await vi.advanceTimersByTimeAsync(5_000);
    expect(mocks.refreshAccessToken).toHaveBeenCalledWith("11111111-1111-1111-1111-111111111111");
    expect(mocks.fetchReplayPage.mock.calls[1][0].accessToken).toBe(accessToken);
    await response.body!.cancel();
  });

  it("closes SSE when its refresh session has been revoked", async () => {
    const expired = "header." + Buffer.from(JSON.stringify({ exp: 1 })).toString("base64url") + ".signature";
    mocks.auth.mockImplementation(async (handler?: (request: object) => Promise<Response>) => handler
      ? async () => handler({ auth: { userId: "user-1", accessToken: expired } }) : { userId: "user-1" });
    const live = connection(() => false);
    mocks.registerConnection.mockResolvedValue(live);
    mocks.refreshAccessToken.mockResolvedValue({ status: "failure", error: "RefreshTokenMissing" });
    mocks.fetchReplayPage.mockResolvedValue(emptyPage);
    const response = await open();
    await vi.advanceTimersByTimeAsync(5_000);
    expect(live.close).toHaveBeenCalledOnce();
    expect(mocks.fetchReplayPage).toHaveBeenCalledOnce();
    await response.body!.cancel();
  });
});
