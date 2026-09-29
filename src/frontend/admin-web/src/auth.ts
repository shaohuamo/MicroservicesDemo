import NextAuth, { customFetch } from "next-auth";
import type { OIDCConfig } from "@auth/core/providers";
import type { Profile } from "next-auth";
import { denylistAccessToken } from "@/lib/auth/access-token-denylist";
import {
  fetchIdentityServer,
  getFrontendClientId,
  getFrontendClientSecret,
  getIdentityServerPublicUrl,
} from "@/lib/auth/identity-server-client";
import {
  createRefreshTokenRecord,
  deleteRefreshTokenRecord,
} from "@/lib/auth/refresh-token-store";
import { getOrRefreshAccessToken, revokeRefreshSession } from "@/lib/auth/refresh-coordinator";
import { logDevelopmentHttp } from "@/lib/dev-http-logging";

type IdentityServerProfile = Profile & {
  preferred_username?: string;
};

const ACCESS_TOKEN_REFRESH_SKEW_SECONDS = 60;

const identityServerProvider: OIDCConfig<IdentityServerProfile> = {
  id: "identity-server",
  name: "IdentityServer",
  type: "oidc",
  issuer: getIdentityServerPublicUrl(),
  wellKnown: `${getIdentityServerPublicUrl()}/.well-known/openid-configuration`,
  clientId: getFrontendClientId(),
  clientSecret: getFrontendClientSecret(),
  authorization: {
    params: {
      scope: "openid profile email products-api notifications-api offline_access",
    },
  },
  checks: ["pkce", "state"],
  profile(profile) {
    const id = profile.sub;
    if (!id) {
      throw new Error("IdentityServer OIDC profile is missing sub.");
    }
    const name = profile.preferred_username ?? profile.name ?? profile.sub ?? undefined;

    return {
      id,
      name,
      email: profile.email ?? undefined,
    };
  },
  [customFetch]: fetchIdentityServer,
};

export const { handlers, auth, signIn, signOut } = NextAuth((request) => ({
  trustHost: true,
  secret: process.env.AUTH_SECRET,
  session: {
    strategy: "jwt",
  },
  providers: [identityServerProvider],
  events: {
    async signOut(message) {
      if (!("token" in message) || !message.token) {
        return;
      }

      const accessToken = typeof message.token.accessToken === "string"
        ? message.token.accessToken
        : undefined;
      const refreshTokenRecordId = typeof message.token.refreshTokenRecordId === "string"
        ? message.token.refreshTokenRecordId
        : undefined;

      let cachedAccessToken: string | undefined;
      let revokeError: unknown;
      if (refreshTokenRecordId) {
        try {
          cachedAccessToken = await revokeRefreshSession(refreshTokenRecordId);
        } catch (error) {
          revokeError = error;
        }
      }

      const tokensToDenylist = new Set([accessToken, cachedAccessToken].filter(
        (value): value is string => typeof value === "string",
      ));
      const operations = await Promise.allSettled([
        ...[...tokensToDenylist].map(denylistAccessToken),
        refreshTokenRecordId ? deleteRefreshTokenRecord(refreshTokenRecordId) : Promise.resolve(),
      ]);
      if (revokeError) throw revokeError;
      const failed = operations.find((operation) => operation.status === "rejected");
      if (failed?.status === "rejected") throw failed.reason;
    },
  },
  callbacks: {
    async redirect({ url, baseUrl }) {
      const identityServerPublicUrl = getIdentityServerPublicUrl();

      if (url.startsWith(identityServerPublicUrl)) {
        return url;
      }

      if (url.startsWith("/")) {
        return `${baseUrl}${url}`;
      }

      if (new URL(url).origin === baseUrl) {
        return url;
      }

      return baseUrl;
    },
    // token is Auth.js' internal JWT session payload. It is encrypted/signed into
    // the session cookie. Keep accessToken here, but store refreshToken in PostgreSQL
    // and keep only refreshTokenRecordId in this cookie payload.
    async jwt({ token, account }) {
      if (account) {
        // Auth.js assigns a separate random user.id for OAuth sign-ins.
        // providerAccountId is the IdentityServer profile sub used by the APIs.
        token.sub = account.providerAccountId;
      }

      if (account?.access_token) {
        token.accessToken = account.access_token;
      }

      if (account?.id_token) {
        token.idToken = account.id_token;
      }

      if (account?.refresh_token) {
        const userId = token.sub;

        if (userId) {
          token.refreshTokenRecordId = await createRefreshTokenRecord(userId, account.refresh_token);
        } else {
          token.error = "RefreshTokenStoreError";
        }
      }

      if (account) {
        logDevelopmentHttp("identityserver sign-in token response", {
          provider: account.provider,
          type: account.type,
          accessToken: account.access_token,
          refreshToken: account.refresh_token,
          idToken: account.id_token,
          expiresAt: account.expires_at,
          expiresIn: account.expires_in,
          scope: account.scope,
          tokenType: account.token_type,
        });
      }

      if (account?.expires_at) {
        token.accessTokenExpiresAt = account.expires_at;
      } else if (account?.expires_in) {
        token.accessTokenExpiresAt = Math.floor(Date.now() / 1000) + account.expires_in;
      }

      token.error = undefined;

      const path = request?.nextUrl.pathname;
      const needsBackendAccessToken = path?.startsWith("/api/")
        && !["/api/auth", "/api/health", "/api/culture"].some(
          (excluded) => path === excluded || path.startsWith(`${excluded}/`),
        );

      if (!needsBackendAccessToken) {
        return token;
      }

      const accessTokenExpiresAt = typeof token.accessTokenExpiresAt === "number"
        ? token.accessTokenExpiresAt
        : undefined;

      if (!accessTokenExpiresAt) {
        return token;
      }

      const shouldRefreshAccessToken =
        Date.now() >= (accessTokenExpiresAt - ACCESS_TOKEN_REFRESH_SKEW_SECONDS) * 1000;

      if (!shouldRefreshAccessToken) {
        return token;
      }

      const refreshTokenRecordId = typeof token.refreshTokenRecordId === "string"
        ? token.refreshTokenRecordId
        : undefined;

      if (!refreshTokenRecordId) {
        token.error = "RefreshTokenMissing";
        return token;
      }

      try {
        const result = await getOrRefreshAccessToken(refreshTokenRecordId);
        if (result.status === "failure") {
          token.error = result.error;
          return token;
        }
        token.accessToken = result.accessToken;
        token.idToken = result.idToken ?? token.idToken;
        token.accessTokenExpiresAt = result.expiresAt;
        return token;
      } catch {
        token.error = "RefreshUnavailable";
        return token;
      }
    },
    // session is the application-facing object returned by auth()/useSession().
    // Only copy fields that the app needs; do not expose refreshToken here.
    session({ session, token }) {
      if (typeof token.sub === "string" && token.sub.trim()) {
        session.userId = token.sub;
      }
      session.accessToken = typeof token.accessToken === "string" ? token.accessToken : undefined;
      session.idToken = typeof token.idToken === "string" ? token.idToken : undefined;
      session.error = token.error === "RefreshTokenMissing"
        || token.error === "RefreshAccessTokenError"
        || token.error === "RefreshTokenStoreError"
        || token.error === "RefreshUnavailable"
        ? token.error
        : undefined;
      return session;
    },
  },
}));
