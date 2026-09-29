import type { DefaultSession } from "next-auth";

declare module "next-auth" {
  interface Session {
    userId?: string;
    accessToken?: string;
    idToken?: string;
    error?: "RefreshTokenMissing" | "RefreshAccessTokenError" | "RefreshTokenStoreError" | "RefreshUnavailable";
    user?: DefaultSession["user"];
  }
}

declare module "next-auth/jwt" {
  interface JWT {
    accessToken?: string;
    idToken?: string;
    refreshTokenRecordId?: string;
    accessTokenExpiresAt?: number;
    error?: "RefreshTokenMissing" | "RefreshAccessTokenError" | "RefreshTokenStoreError" | "RefreshUnavailable";
  }
}
