import { afterEach, describe, expect, it, vi } from "vitest";
import type { NextRequest } from "next/server";
import { POST, PUT, DELETE } from "@/app/api/[...path]/route";

vi.mock("@/lib/auth/gateway-session-proof", () => ({
  GATEWAY_PROOF_HEADERS: ["x-admin-session-id", "x-admin-proof-iat", "x-admin-proof-kid", "x-admin-proof-sig"],
  getRefreshTokenRecordId: vi.fn(async () => "11111111-1111-1111-1111-111111111111"),
  applyGatewaySessionProof: vi.fn(),
}));

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
  method = "POST",
}: {
  headers: Record<string, string>;
  body?: string;
  method?: string;
}) {
  return {
    method,
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
          authorization: "Bearer browser-supplied-token",
          "x-admin-proof-sig": "browser-forgery",
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
    expect(headers.get("x-admin-proof-sig")).toBeNull();
  });

  it.each(["POST", "PUT", "DELETE"])("forwards the idempotency key and replay response unchanged for %s", async (method) => {
    vi.stubEnv("API_GATEWAY_INTERNAL_URL", "http://apigateway");
    vi.stubEnv("FRONTEND_PUBLIC_URL", "https://250669.xyz");
    const key = "7f277273-b334-47f9-8b59-d37aa3473665";
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ productId: "p1" }), {
        status: method === "POST" ? 201 : 200,
        headers: { "idempotency-replayed": "true", "idempotency-outcome": "replayed" },
      }),
    );

    const handler = { POST, PUT, DELETE }[method as "POST" | "PUT" | "DELETE"];
    const response = await handler(
      createProxyRequest({
        method,
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
    expect(response.headers.get("idempotency-replayed")).toBe("true");
    expect(response.headers.get("idempotency-outcome")).toBe("replayed");
  });
});
