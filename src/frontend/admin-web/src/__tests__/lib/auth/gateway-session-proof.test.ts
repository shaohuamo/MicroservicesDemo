// @vitest-environment node

import { createHmac, createHash } from "node:crypto";
import { afterEach, describe, expect, it, vi } from "vitest";
import { applyGatewaySessionProof, getRefreshTokenRecordId } from "@/lib/auth/gateway-session-proof";

const mocks = vi.hoisted(() => ({ getToken: vi.fn() }));
vi.mock("next-auth/jwt", () => ({ getToken: mocks.getToken }));

const recordId = "11111111-1111-1111-1111-111111111111";
const key = Buffer.from("0123456789abcdef0123456789abcdef");

describe("gateway session proof", () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllEnvs();
    mocks.getToken.mockReset();
  });

  it("reads the record ID only from the Auth.js cookie", async () => {
    vi.stubEnv("AUTH_SECRET", "auth-cookie-secret");
    vi.stubEnv("AUTH_URL", "https://example.com");
    mocks.getToken.mockResolvedValue({ refreshTokenRecordId: recordId });
    const id = await getRefreshTokenRecordId(new Request("https://example.com/api/products", {
      headers: { cookie: "__Secure-authjs.session-token=encrypted", authorization: "Bearer attacker" },
    }));

    expect(id).toBe(recordId);
    const options = mocks.getToken.mock.calls[0][0];
    expect(options.secureCookie).toBe(true);
    expect(options.req.headers.get("authorization")).toBeNull();
    expect(options.req.headers.get("cookie")).toContain("authjs.session-token");
  });

  it("signs the exact forwarded access token and replaces untrusted proof headers", () => {
    vi.stubEnv("AUTH_GATEWAY_PROOF_KEY_ID", "v1");
    vi.stubEnv("AUTH_GATEWAY_PROOF_KEY", key.toString("base64"));
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-29T12:00:00Z"));
    const headers = new Headers({ "x-admin-proof-sig": "forged" });
    applyGatewaySessionProof(headers, recordId, "new-access-token");

    const timestamp = "1790683200";
    const tokenHash = createHash("sha256").update("new-access-token").digest("base64url");
    const expected = createHmac("sha256", key)
      .update(`v1\n${recordId}\n${timestamp}\n${tokenHash}`)
      .digest("base64url");
    expect(headers.get("x-admin-proof-iat")).toBe(timestamp);
    expect(headers.get("x-admin-proof-sig")).toBe(expected);
    applyGatewaySessionProof(headers, recordId, "refreshed-access-token");
    expect(headers.get("x-admin-proof-sig")).not.toBe(expected);
  });
});
