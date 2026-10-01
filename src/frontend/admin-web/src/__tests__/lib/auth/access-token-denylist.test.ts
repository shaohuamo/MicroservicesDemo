// @vitest-environment node

import { describe, expect, it } from "vitest";
import { getAccessTokenDenylistTtlSeconds } from "@/lib/auth/access-token-denylist";

function accessToken(jti: string, exp: number) {
  return `header.${Buffer.from(JSON.stringify({ jti, exp })).toString("base64url")}.signature`;
}

describe("access token denylist TTL", () => {
  it("keeps the key through the JWT's 30-second validation skew", () => {
    expect(getAccessTokenDenylistTtlSeconds(accessToken("jti-1", 1900), 1000)).toBe(930);
    expect(getAccessTokenDenylistTtlSeconds(accessToken("jti-1", 999), 1000)).toBe(29);
    expect(getAccessTokenDenylistTtlSeconds(accessToken("jti-1", 970), 1000)).toBe(0);
  });
});
