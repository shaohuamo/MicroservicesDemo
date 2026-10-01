// @vitest-environment node

import { EventEmitter } from "node:events";
import { afterEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({ createClient: vi.fn() }));
vi.mock("server-only", () => ({}));
vi.mock("redis", () => ({ createClient: mocks.createClient }));

import { registerNotificationConnection } from "@/lib/notifications/server-hub";

class FakeRedis extends EventEmitter {
  isReady = false;
  isOpen = true;
  connect = vi.fn<() => Promise<void>>();
  subscribe = vi.fn(async () => undefined);
  zAdd = vi.fn(async () => 1);
  expire = vi.fn(async () => true);
  zRem = vi.fn(async () => 1);
  destroy = vi.fn();
  duplicate = vi.fn<() => FakeRedis>();
}

function redisPair() {
  const redis = new FakeRedis();
  const subscriber = new FakeRedis();
  redis.duplicate.mockReturnValue(subscriber);
  mocks.createClient.mockReturnValue(redis);
  return { redis, subscriber };
}

function readyRedisPair() {
  const clients = redisPair();
  clients.redis.connect.mockImplementation(async () => { clients.redis.isReady = true; });
  clients.subscriber.connect.mockImplementation(async () => { clients.subscriber.isReady = true; });
  return clients;
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((complete) => { resolve = complete; });
  return { promise, resolve };
}

describe("notification hub Redis fallback", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
    globalThis.adminWebNotificationHub = undefined;
    mocks.createClient.mockReset();
  });

  it("registers the SSE locally while Redis is down and restores presence after subscribing", async () => {
    const redis = new FakeRedis();
    const subscriber = new FakeRedis();
    let connectRedis!: () => void;
    let connectSubscriber!: () => void;
    redis.connect.mockReturnValue(new Promise<void>((resolve) => { connectRedis = resolve; }));
    subscriber.connect.mockReturnValue(new Promise<void>((resolve) => { connectSubscriber = resolve; }));
    redis.duplicate.mockReturnValue(subscriber);
    mocks.createClient.mockReturnValue(redis);

    const connection = await registerNotificationConnection("user-1", vi.fn());
    expect(connection.isRealtimeAvailable()).toBe(false);
    expect(redis.zAdd).not.toHaveBeenCalled();

    redis.isReady = true;
    subscriber.isReady = true;
    connectRedis();
    connectSubscriber();
    await vi.waitFor(() => expect(connection.isRealtimeAvailable()).toBe(true));
    expect(subscriber.subscribe).toHaveBeenCalledOnce();
    expect(redis.zAdd).toHaveBeenCalledOnce();

    subscriber.isReady = false;
    subscriber.emit("reconnecting");
    expect(connection.isRealtimeAvailable()).toBe(false);
    expect(connection.getDisconnectGeneration()).toBe(1);

    subscriber.isReady = true;
    subscriber.emit("ready");
    await vi.waitFor(() => expect(connection.isRealtimeAvailable()).toBe(true));
    expect(redis.zAdd).toHaveBeenCalledTimes(2);

    await connection.close();
    expect(redis.zRem).toHaveBeenCalledOnce();
  });

  it("waits for delayed presence without creating another Redis initialization", async () => {
    vi.useFakeTimers();
    const { redis, subscriber } = readyRedisPair();
    const presence = deferred<number>();
    redis.zAdd.mockReturnValueOnce(presence.promise);
    const connection = await registerNotificationConnection("user-1", vi.fn());
    const controller = new AbortController();
    const removeAbortListener = vi.spyOn(controller.signal, "removeEventListener");
    let settled = false;
    const ready = connection.waitForRealtimeReady(1_000, controller.signal).then((result) => {
      settled = true;
      return result;
    });

    await vi.advanceTimersByTimeAsync(500);
    expect(subscriber.subscribe).toHaveBeenCalledOnce();
    expect(connection.isRealtimeAvailable()).toBe(false);
    expect(settled).toBe(false);
    presence.resolve(1);
    await expect(ready).resolves.toBe(true);

    expect(mocks.createClient).toHaveBeenCalledOnce();
    expect(redis.connect).toHaveBeenCalledOnce();
    expect(subscriber.connect).toHaveBeenCalledOnce();
    expect(redis.zAdd).toHaveBeenCalledOnce();
    expect(vi.getTimerCount()).toBe(0);
    expect(removeAbortListener).toHaveBeenCalledWith("abort", expect.any(Function));
    expect(globalThis.adminWebNotificationHub?.connectionsByUser.get("user-1")
      ?.get(connection.connectionId)?.realtimeReadyListeners.size).toBe(0);
    await connection.close();
  });

  it("returns immediately without a timer when the realtime connection is already ready", async () => {
    vi.useFakeTimers();
    readyRedisPair();
    const connection = await registerNotificationConnection("user-1", vi.fn());
    await vi.advanceTimersByTimeAsync(0);
    expect(connection.isRealtimeAvailable()).toBe(true);
    const setTimeoutSpy = vi.spyOn(globalThis, "setTimeout");

    await expect(connection.waitForRealtimeReady(1_000, new AbortController().signal)).resolves.toBe(true);
    expect(setTimeoutSpy).not.toHaveBeenCalled();
    await connection.close();
  });

  it("times out after one second and lets the existing initialization finish later", async () => {
    vi.useFakeTimers();
    const { redis, subscriber } = redisPair();
    const redisConnect = deferred<void>();
    const subscriberConnect = deferred<void>();
    redis.connect.mockReturnValue(redisConnect.promise);
    subscriber.connect.mockReturnValue(subscriberConnect.promise);
    const connection = await registerNotificationConnection("user-1", vi.fn());
    const controller = new AbortController();
    const removeAbortListener = vi.spyOn(controller.signal, "removeEventListener");
    let settled = false;
    const ready = connection.waitForRealtimeReady(1_000, controller.signal).then((result) => {
      settled = true;
      return result;
    });

    await vi.advanceTimersByTimeAsync(999);
    expect(settled).toBe(false);
    await vi.advanceTimersByTimeAsync(1);
    await expect(ready).resolves.toBe(false);
    expect(vi.getTimerCount()).toBe(0);
    expect(removeAbortListener).toHaveBeenCalledWith("abort", expect.any(Function));
    expect(globalThis.adminWebNotificationHub?.connectionsByUser.get("user-1")
      ?.get(connection.connectionId)?.realtimeReadyListeners.size).toBe(0);

    redis.isReady = true;
    subscriber.isReady = true;
    redisConnect.resolve(undefined);
    subscriberConnect.resolve(undefined);
    await vi.advanceTimersByTimeAsync(0);
    expect(connection.isRealtimeAvailable()).toBe(true);
    expect(mocks.createClient).toHaveBeenCalledOnce();
    await connection.close();
  });

  it("cleans readiness listeners and the timer when the request is aborted", async () => {
    vi.useFakeTimers();
    const { redis, subscriber } = redisPair();
    redis.connect.mockReturnValue(new Promise<void>(() => undefined));
    subscriber.connect.mockReturnValue(new Promise<void>(() => undefined));
    const connection = await registerNotificationConnection("user-1", vi.fn());
    const controller = new AbortController();
    const removeAbortListener = vi.spyOn(controller.signal, "removeEventListener");
    const rejected = expect(connection.waitForRealtimeReady(1_000, controller.signal))
      .rejects.toMatchObject({ name: "AbortError" });
    controller.abort();
    await rejected;

    expect(vi.getTimerCount()).toBe(0);
    expect(removeAbortListener).toHaveBeenCalledWith("abort", expect.any(Function));
    expect(globalThis.adminWebNotificationHub?.connectionsByUser.get("user-1")
      ?.get(connection.connectionId)?.realtimeReadyListeners.size).toBe(0);
    await connection.close();
  });

  it("finishes pending readiness waits when the local connection closes", async () => {
    vi.useFakeTimers();
    const { redis, subscriber } = redisPair();
    redis.connect.mockReturnValue(new Promise<void>(() => undefined));
    subscriber.connect.mockReturnValue(new Promise<void>(() => undefined));
    const connection = await registerNotificationConnection("user-1", vi.fn());
    const controller = new AbortController();
    const removeAbortListener = vi.spyOn(controller.signal, "removeEventListener");
    const ready = connection.waitForRealtimeReady(1_000, controller.signal);

    await connection.close();
    await expect(ready).resolves.toBe(false);
    expect(connection.isRealtimeAvailable()).toBe(false);
    expect(vi.getTimerCount()).toBe(0);
    expect(removeAbortListener).toHaveBeenCalledWith("abort", expect.any(Function));
    expect(globalThis.adminWebNotificationHub?.connectionsByUser.size).toBe(0);
  });
});
