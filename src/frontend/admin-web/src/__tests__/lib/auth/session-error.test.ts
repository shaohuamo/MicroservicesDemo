// @vitest-environment node

import { describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  configure: undefined as unknown,
}));

vi.mock("next-auth", () => ({
  default: (configure: unknown) => {
    mocks.configure = configure;
    return { handlers: {}, auth: vi.fn(), signIn: vi.fn(), signOut: vi.fn() };
  },
  customFetch: Symbol("customFetch"),
}));
vi.mock("@/lib/auth/access-token-denylist", () => ({ denylistAccessToken: vi.fn() }));
vi.mock("@/lib/auth/identity-server-client", () => ({
  fetchIdentityServer: vi.fn(),
  getFrontendClientId: () => "test_app",
  getFrontendClientSecret: () => "secret",
  getIdentityServerPublicUrl: () => "http://localhost:8485",
}));
vi.mock("@/lib/auth/refresh-token-store", () => ({
  createRefreshTokenRecord: vi.fn(),
  deleteRefreshTokenRecord: vi.fn(),
}));
vi.mock("@/lib/auth/refresh-coordinator", () => ({
  getOrRefreshAccessToken: vi.fn(),
  revokeRefreshSession: vi.fn(),
}));
vi.mock("@/lib/dev-http-logging", () => ({ logDevelopmentHttp: vi.fn() }));

import "@/auth";

type TestToken = {
  accessToken: string;
  accessTokenExpiresAt: number;
  refreshTokenRecordId: string;
  error?: "RefreshTokenMissing" | "RefreshUnavailable";
};

function jwtForPageRender(token: TestToken) {
  const configure = mocks.configure as () => {
    callbacks: { jwt: (args: { token: TestToken }) => Promise<TestToken> };
  };
  return configure().callbacks.jwt({ token });
}

describe("session refresh errors", () => {
  it("preserves a terminal refresh error when rendering a page", async () => {
    const token: TestToken = {
      accessToken: "expired-access-token",
      accessTokenExpiresAt: Math.floor(Date.now() / 1_000) - 120,
      refreshTokenRecordId: "11111111-1111-1111-1111-111111111111",
      error: "RefreshTokenMissing",
    };

    await expect(jwtForPageRender(token)).resolves.toMatchObject({
      error: "RefreshTokenMissing",
    });
  });
});
