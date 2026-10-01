// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";
vi.mock("server-only", () => ({}));
vi.mock("@/lib/auth/gateway-session-proof", () => ({ applyGatewaySessionProof: vi.fn() }));
vi.mock("@/lib/notifications/server-otel", () => ({
  getServerTracer: () => ({ startActiveSpan: (_name: string, _options: object, callback: (span: object) => unknown) => callback({ setAttribute: vi.fn(), setStatus: vi.fn(), end: vi.fn() }) }),
  logServerEvent: vi.fn(), SeverityNumber: { INFO: 9, WARN: 13, ERROR: 17 },
}));
import { fetchNotificationReplayPage } from "@/lib/notifications/server-gateway";
const query = { accessToken: "access", refreshTokenRecordId: "session" };
afterEach(() => { vi.unstubAllGlobals(); vi.unstubAllEnvs(); });

describe("descending notification replay gateway", () => {
  it("requests the whole unacknowledged range without a browser receive cursor", async () => {
    vi.stubEnv("API_GATEWAY_INTERNAL_URL", "http://gateway:8080");
    const fetch = vi.fn<(url: string) => Promise<Response>>(async () => Response.json({ items: [], nextCursor: null, watermark: 200 }));
    vi.stubGlobal("fetch", fetch);
    expect(await fetchNotificationReplayPage(query)).toEqual({ items: [], nextCursor: null, watermark: 200 });
    const url = new URL(fetch.mock.calls[0][0] as string);
    expect(url.searchParams.get("limit")).toBe("100");
    expect(url.searchParams.has("afterSequence")).toBe(false);
    expect(url.searchParams.has("cursor")).toBe(false);
  });

  it("forwards opaque cursors verbatim and preserves explicit end-of-pagination", async () => {
    vi.stubEnv("API_GATEWAY_INTERNAL_URL", "http://gateway:8080");
    const cursor = "opaque+cursor/with=padding";
    const fetch = vi.fn<(url: string) => Promise<Response>>(async () => Response.json({ items: [], nextCursor: null, watermark: 200 }));
    vi.stubGlobal("fetch", fetch);
    const page = await fetchNotificationReplayPage({ ...query, cursor });
    expect(new URL(fetch.mock.calls[0][0] as string).searchParams.get("cursor")).toBe(cursor);
    expect(page.nextCursor).toBeNull();
  });

  it.each([
    { items: [], watermark: 200 },
    { items: [], nextCursor: 100, watermark: 200 },
    { items: [], nextCursor: "", watermark: 200 },
    { items: [], nextCursor: null, watermark: null },
  ])("rejects an invalid page rather than silently stopping replay", async (page) => {
    vi.stubEnv("API_GATEWAY_INTERNAL_URL", "http://gateway:8080");
    vi.stubGlobal("fetch", vi.fn(async () => Response.json(page)));
    await expect(fetchNotificationReplayPage(query)).rejects.toMatchObject({ status: 503 });
  });
});
