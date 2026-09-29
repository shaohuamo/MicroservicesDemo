import { afterEach, describe, expect, it, vi } from "vitest";
import type { NextRequest } from "next/server";
import { POST } from "@/app/api/[...path]/route";

type RouteContext = { params: Promise<{ path: string[] }> };

vi.mock("@/auth", () => ({
  auth: vi.fn(async (handler: (request: NextRequest & { auth: { accessToken: string } }, context: RouteContext) => Promise<Response>) =>
    (request: NextRequest, context: RouteContext) =>
    handler(Object.assign(request, { auth: { accessToken: "access-token" } }), context)),
}));

const context = {
  params: Promise.resolve({
    path: ["products"],
  }),
};

function createProxyRequest({
  headers,
  body,
}: {
  headers: Record<string, string>;
  body?: string;
}) {
  return {
    method: "POST",
    headers: new Headers(headers),
    nextUrl: new URL("https://250669.xyz/api/products"),
    arrayBuffer: async () => new TextEncoder().encode(body ?? "").buffer,
  } as unknown as NextRequest;
}

describe("admin API proxy CSRF protection", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllEnvs();
  });

  it("rejects unsafe requests from a foreign origin", async () => {
    vi.stubEnv("API_GATEWAY_INTERNAL_URL", "http://apigateway");
    vi.stubEnv("FRONTEND_PUBLIC_URL", "https://250669.xyz");
    const fetchMock = vi.spyOn(globalThis, "fetch");

    const response = await POST(
      createProxyRequest({
        headers: {
          referer: "https://attacker.example/products",
        },
      }),
      context,
    );

    await expect(response.json()).resolves.toEqual({
      message: "Cross-site request rejected.",
    });
    expect(response.status).toBe(403);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("allows unsafe requests from the configured frontend origin", async () => {
    vi.stubEnv("API_GATEWAY_INTERNAL_URL", "http://apigateway");
    vi.stubEnv("FRONTEND_PUBLIC_URL", "https://250669.xyz");
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify([{ productId: "p1" }]), {
        status: 200,
        headers: {
          "content-type": "application/json",
        },
      }),
    );

    const request = createProxyRequest({
      headers: {
        referer: "https://250669.xyz/products",
        "content-type": "application/json",
      },
      body: JSON.stringify({ displayName: "Coffee" }),
    });
    expect(request.headers.get("referer")).toBe("https://250669.xyz/products");

    const response = await POST(request, context);

    expect(response.status).toBe(200);
    expect(fetchMock).toHaveBeenCalledWith(
      new URL("http://apigateway/gateway/products"),
      expect.objectContaining({
        method: "POST",
        cache: "no-store",
        redirect: "manual",
      }),
    );
  });

  it("does not forward browser cookies to the gateway", async () => {
    vi.stubEnv("API_GATEWAY_INTERNAL_URL", "http://apigateway");
    vi.stubEnv("FRONTEND_PUBLIC_URL", "https://250669.xyz");
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify([{ productId: "p1" }]), {
        status: 200,
        headers: {
          "content-type": "application/json",
        },
      }),
    );

    const response = await POST(
      createProxyRequest({
        headers: {
          referer: "https://250669.xyz/products",
          cookie: "authjs.session-token=secret; MicroservicesDemo.Culture=c%3Den",
        },
        body: JSON.stringify({ displayName: "Coffee" }),
      }),
      context,
    );

    expect(response.status).toBe(200);
    const [, init] = fetchMock.mock.calls[0];
    const headers = init?.headers as Headers;
    expect(headers.get("cookie")).toBeNull();
    expect(headers.get("authorization")).toBe("Bearer access-token");
  });

  it("forwards the idempotency key unchanged", async () => {
    vi.stubEnv("API_GATEWAY_INTERNAL_URL", "http://apigateway");
    vi.stubEnv("FRONTEND_PUBLIC_URL", "https://250669.xyz");
    const key = "7f277273-b334-47f9-8b59-d37aa3473665";
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ productId: "p1" }), { status: 201 }),
    );

    await POST(
      createProxyRequest({
        headers: {
          referer: "https://250669.xyz/products",
          "content-type": "application/json",
          "idempotency-key": key,
        },
        body: JSON.stringify({ displayName: "Coffee" }),
      }),
      context,
    );

    const [, init] = fetchMock.mock.calls[0];
    expect((init?.headers as Headers).get("idempotency-key")).toBe(key);
  });
});
