import {
  context,
  propagation,
  SpanKind,
  SpanStatusCode,
  type Span,
} from "@opentelemetry/api";
import {
  getBodyForLog,
  getHeadersForLog,
  getMethodForLog,
  getResponseBodyForLog,
  getUrlForLog,
  logDevelopmentHttp,
} from "@/lib/dev-http-logging";
import {
  getServerTracer,
  logServerEvent,
  SeverityNumber,
} from "@/lib/notifications/server-otel";

export type TokenRefreshResponse = {
  access_token?: string;
  expires_in?: number;
  refresh_token?: string;
  id_token?: string;
  error?: string;
  error_description?: string;
};

export class IdentityServerTokenRefreshError extends Error {
  constructor(
    message: string,
    readonly requiresSignIn: boolean,
  ) {
    super(message);
    this.name = "IdentityServerTokenRefreshError";
  }
}

function trimTrailingSlash(value: string) {
  return value.endsWith("/") ? value.slice(0, -1) : value;
}

export function getIdentityServerPublicUrl() {
  return trimTrailingSlash(process.env.IDENTITYSERVER_PUBLIC_URL || "http://localhost:8085");
}

export function getIdentityServerInternalUrl() {
  return trimTrailingSlash(process.env.IDENTITYSERVER_INTERNAL_URL || getIdentityServerPublicUrl());
}

export function getFrontendClientId() {
  return process.env.IDENTITYSERVER_FRONTEND_CLIENT_ID || "test_app";
}

export function getFrontendClientSecret() {
  return process.env.IDENTITYSERVER_FRONTEND_CLIENT_SECRET || "frontend-secret";
}

function getRequestUrl(input: Parameters<typeof fetch>[0]) {
  if (typeof input === "string" || input instanceof URL) {
    return new URL(input);
  }

  return new URL(input.url);
}

export function rewriteIdentityServerUrl(input: Parameters<typeof fetch>[0]) {
  const publicUrl = getIdentityServerPublicUrl();
  const internalUrl = getIdentityServerInternalUrl();
  const url = getRequestUrl(input);

  if (url.origin === publicUrl) {
    return `${internalUrl}${url.pathname}${url.search}`;
  }

  return input;
}

function replaceOrigin(value: unknown, from: string, to: string): unknown {
  if (typeof value === "string") {
    return value.startsWith(from) ? `${to}${value.slice(from.length)}` : value;
  }

  if (Array.isArray(value)) {
    return value.map((item) => replaceOrigin(item, from, to));
  }

  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value).map(([key, item]) => [key, replaceOrigin(item, from, to)]),
    );
  }

  return value;
}

function getRequestMethod(input: Parameters<typeof fetch>[0], init?: Parameters<typeof fetch>[1]) {
  if (init?.method) {
    return init.method.toUpperCase();
  }

  return input instanceof Request ? input.method.toUpperCase() : "GET";
}

function createTraceHeaders(input: Parameters<typeof fetch>[0], init?: Parameters<typeof fetch>[1]) {
  const headers = new Headers(input instanceof Request ? input.headers : undefined);

  if (init?.headers) {
    new Headers(init.headers).forEach((value, key) => headers.set(key, value));
  }

  propagation.inject(context.active(), headers, {
    set(carrier, key, value) {
      carrier.set(key, value);
    },
  });

  return headers;
}

function setRequestAttributes(span: Span, url: URL, method: string) {
  span.setAttributes({
    "http.request.method": method,
    "url.scheme": url.protocol.slice(0, -1),
    "server.address": url.hostname,
    ...(url.port ? { "server.port": Number(url.port) } : {}),
    "url.path": url.pathname,
  });
}

/**
 * Sends a server-side request to IdentityServer with the active W3C trace context.
 * Request bodies, credentials, and query strings are deliberately excluded from span attributes.
 */
export async function fetchIdentityServerWithTrace(
  input: Parameters<typeof fetch>[0],
  init?: Parameters<typeof fetch>[1],
) {
  const rewrittenInput = rewriteIdentityServerUrl(input);
  const targetUrl = getRequestUrl(rewrittenInput);
  const method = getRequestMethod(input, init);
  const tracer = getServerTracer();

  return tracer.startActiveSpan(
    `identityserver ${method} ${targetUrl.pathname}`,
    { kind: SpanKind.CLIENT },
    async (span) => {
      setRequestAttributes(span, targetUrl, method);
      logServerEvent(SeverityNumber.INFO, "INFO", "BFF IdentityServer request started", {
        "http.request.method": method,
        "http.route": targetUrl.pathname,
        "server.address": targetUrl.hostname,
      });

      try {
        const response = await fetch(rewrittenInput, {
          ...init,
          headers: createTraceHeaders(input, init),
        });

        span.setAttribute("http.response.status_code", response.status);

        if (!response.ok) {
          span.setStatus({ code: SpanStatusCode.ERROR, message: `HTTP ${response.status}` });
        }

        logServerEvent(
          response.ok ? SeverityNumber.INFO : SeverityNumber.WARN,
          response.ok ? "INFO" : "WARN",
          "BFF IdentityServer request completed",
          {
            "http.request.method": method,
            "http.route": targetUrl.pathname,
            "http.response.status_code": response.status,
            "server.address": targetUrl.hostname,
          },
        );

        return response;
      } catch (error) {
        span.recordException(error instanceof Error ? error : new Error(String(error)));
        span.setStatus({ code: SpanStatusCode.ERROR });
        logServerEvent(SeverityNumber.ERROR, "ERROR", "BFF IdentityServer request failed", {
          "http.request.method": method,
          "http.route": targetUrl.pathname,
          "server.address": targetUrl.hostname,
          "error.type": error instanceof Error ? error.name : "UnknownError",
        });
        throw error;
      } finally {
        span.end();
      }
    },
  );
}

export async function fetchIdentityServer(
  input: Parameters<typeof fetch>[0],
  init?: Parameters<typeof fetch>[1],
) {
  const publicUrl = getIdentityServerPublicUrl();
  const internalUrl = getIdentityServerInternalUrl();
  const rewrittenInput = rewriteIdentityServerUrl(input);

  logDevelopmentHttp("identityserver request", {
    method: getMethodForLog(input, init),
    url: getUrlForLog(input),
    rewrittenUrl: getUrlForLog(rewrittenInput),
    headers: getHeadersForLog(init?.headers),
    body: getBodyForLog(init?.body),
  });

  const response = await fetchIdentityServerWithTrace(input, init);
  const url = getRequestUrl(input);

  logDevelopmentHttp("identityserver response", {
    method: getMethodForLog(input, init),
    url: getUrlForLog(input),
    rewrittenUrl: getUrlForLog(rewrittenInput),
    status: response.status,
    statusText: response.statusText,
    headers: getHeadersForLog(response.headers),
    body: await getResponseBodyForLog(response),
  });

  if (url.pathname !== "/.well-known/openid-configuration") {
    return response;
  }

  const metadata = await response.json();
  const publicMetadata = replaceOrigin(metadata, internalUrl, publicUrl);

  return new Response(JSON.stringify(publicMetadata), {
    status: response.status,
    statusText: response.statusText,
    headers: {
      "content-type": "application/json",
    },
  });
}

export async function refreshIdentityServerAccessToken(
  refreshToken: string,
  signal?: AbortSignal,
): Promise<TokenRefreshResponse> {
  const tokenEndpoint = `${getIdentityServerPublicUrl()}/connect/token`;
  const requestBody = new URLSearchParams({
    grant_type: "refresh_token",
    refresh_token: refreshToken,
    client_id: getFrontendClientId(),
    client_secret: getFrontendClientSecret(),
  });

  logDevelopmentHttp("identityserver refresh token request", {
    method: "POST",
    url: tokenEndpoint,
    rewrittenUrl: getUrlForLog(rewriteIdentityServerUrl(tokenEndpoint)),
    headers: {
      "content-type": "application/x-www-form-urlencoded",
    },
    body: getBodyForLog(requestBody),
    refreshToken,
  });

  const response = await fetchIdentityServerWithTrace(tokenEndpoint, {
    method: "POST",
    signal,
    headers: {
      "content-type": "application/x-www-form-urlencoded",
    },
    body: requestBody,
  });

  const refreshedToken = await response.json() as TokenRefreshResponse;

  logDevelopmentHttp("identityserver refresh token response", {
    status: response.status,
    statusText: response.statusText,
    accessToken: refreshedToken.access_token,
    refreshToken: refreshedToken.refresh_token,
    idToken: refreshedToken.id_token,
    body: refreshedToken,
  });

  if (!response.ok) {
    throw new IdentityServerTokenRefreshError(
      refreshedToken.error_description ?? refreshedToken.error ?? "Unable to refresh access token.",
      refreshedToken.error === "invalid_grant",
    );
  }

  return refreshedToken;
}
