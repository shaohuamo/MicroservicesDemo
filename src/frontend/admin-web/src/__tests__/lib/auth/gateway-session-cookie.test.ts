// @vitest-environment node

import { encode } from "next-auth/jwt";
import { afterEach, describe, expect, it, vi } from "vitest";
import { getRefreshTokenRecordId } from "@/lib/auth/gateway-session-proof";

const id = "11111111-1111-1111-1111-111111111111";

describe("Auth.js cookie proof source", () => {
  afterEach(() => vi.unstubAllEnvs());

  it.each([
    ["http://localhost:3000", "authjs.session-token"],
    ["https://example.com", "__Secure-authjs.session-token"],
  ])("decrypts the real %s session cookie", async (url, cookieName) => {
    vi.stubEnv("AUTH_SECRET", "independent-auth-js-cookie-secret");
    vi.stubEnv("AUTH_URL", url);
    const cookieValue = await encode({
      token: { refreshTokenRecordId: id },
      secret: process.env.AUTH_SECRET!,
      salt: cookieName,
    });

    const request = new Request(`${url}/api/products`, {
      headers: { cookie: `${cookieName}=${cookieValue}`, authorization: "Bearer attacker" },
    });
    await expect(getRefreshTokenRecordId(request)).resolves.toBe(id);
  });
});
