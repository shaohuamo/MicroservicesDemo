import { describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";
import type { NextFetchEvent } from "next/server";
import { proxy } from "@/proxy";

vi.mock("@/auth", () => ({
  auth: vi.fn(async (handler: (request: NextRequest & { auth: null }, event: NextFetchEvent) => Promise<Response>) =>
    (request: NextRequest, event: NextFetchEvent) =>
      handler(Object.assign(request, { auth: null }), event)),
}));

describe("products proxy", () => {
  it("exports a function and redirects an unauthenticated request", async () => {
    expect(typeof proxy).toBe("function");

    const request = new NextRequest("https://admin.example/products");
    const response = await proxy(request, {} as NextFetchEvent);
    if (!response) throw new Error("Products proxy did not return a response.");

    expect(response.status).toBe(307);
    expect(response.headers.get("location")).toBe(
      "https://admin.example/login?callbackUrl=%2Fproducts",
    );
  });
});
