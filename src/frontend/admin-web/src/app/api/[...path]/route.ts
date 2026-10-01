import { NextRequest, NextResponse } from "next/server";
import { context, propagation, SpanKind, SpanStatusCode } from "@opentelemetry/api";
import { auth } from "@/auth";
import type { NextAuthRequest } from "next-auth";
import {
  getRequestHeadersForLog,
  getResponseBodyForLog,
  logDevelopmentHttp,
} from "@/lib/dev-http-logging";
import {
  getServerTracer,
  logServerEvent,
  resolveRequestTraceContext,
  SeverityNumber,
} from "@/lib/notifications/server-otel";
import { resolveBffProxyRoute } from "@/lib/api/bff-route-template";
import { applyGatewaySessionProof, GATEWAY_PROOF_HEADERS, getRefreshTokenRecordId } from "@/lib/auth/gateway-session-proof";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const METHODS_WITHOUT_BODY = new Set(["GET", "HEAD"]);
const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS"]);

type RouteContext = {
  params: Promise<{
    path: string[];
  }>;
};

function getGatewayBaseUrl() {
  const baseUrl = process.env.API_GATEWAY_INTERNAL_URL;

  if (!baseUrl) {
    return null;
  }

  return baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
}

function trimTrailingSlash(value: string) {
  return value.endsWith("/") ? value.slice(0, -1) : value;
}

function getFrontendPublicOrigin(request: NextRequest) {
  const configuredUrl = process.env.FRONTEND_PUBLIC_URL;

  if (configuredUrl) {
    return new URL(trimTrailingSlash(configuredUrl)).origin;
  }

  return request.nextUrl.origin;
}

function getRequestSourceOrigin(request: NextRequest) {
  const origin = request.headers.get("origin");

  if (origin) {
    return new URL(origin).origin;
  }

  const referer = request.headers.get("referer");

  if (referer) {
    return new URL(referer).origin;
  }

  return null;
}

function isCsrfProtectedRequest(request: NextRequest) {
  return !SAFE_METHODS.has(request.method.toUpperCase());
}

function isSameOriginRequest(request: NextRequest) {
  if (!isCsrfProtectedRequest(request)) {
    return true;
  }

  try {
    return getRequestSourceOrigin(request) === getFrontendPublicOrigin(request);
  } catch {
    return false;
  }
}

function getJwtSubject(accessToken: string | undefined) {
  if (!accessToken) {
    return undefined;
  }

  try {
    const [, payload] = accessToken.split(".");
    if (!payload) {
      return undefined;
    }

    const normalizedPayload = payload.replace(/-/g, "+").replace(/_/g, "/");
    const paddedPayload = normalizedPayload.padEnd(
      Math.ceil(normalizedPayload.length / 4) * 4,
      "="
    );
    const claims = JSON.parse(Buffer.from(paddedPayload, "base64").toString("utf8")) as {
      sub?: unknown;
    };

    return typeof claims.sub === "string" && claims.sub.length > 0
      ? claims.sub
      : undefined;
  } catch {
    return undefined;
  }
}

function buildProxyHeaders(request: NextRequest, accessToken?: string) {
  const headers = new Headers(request.headers);

  headers.delete("host");
  headers.delete("content-length");
  headers.delete("connection");
  headers.delete("cookie");
  headers.delete("client-id");
  headers.delete("authorization");
  for (const name of GATEWAY_PROOF_HEADERS) headers.delete(name);

  if (accessToken) {
    headers.set("authorization", `Bearer ${accessToken}`);
  }

  const clientId = getJwtSubject(accessToken);
  if (clientId) {
    headers.set("client-id", clientId);
  }

  return headers;
}

async function proxyRequestInContext(request: NextRequest, routeContext: RouteContext) {
  const gatewayBaseUrl = getGatewayBaseUrl();

  if (!gatewayBaseUrl) {
    return NextResponse.json(
      { message: "API_GATEWAY_INTERNAL_URL is not configured." },
      { status: 500 }
    );
  }

  if (!isSameOriginRequest(request)) {
    return NextResponse.json(
      { message: "Cross-site request rejected." },
      { status: 403 }
    );
  }

  const response = await authenticatedProxyRequest(request, routeContext);
  if (!response) {
    throw new Error("Authenticated API proxy did not return a response.");
  }
  return response;
}

async function proxyRequestWithSession(request: NextAuthRequest, { params }: RouteContext) {
  const gatewayBaseUrl = getGatewayBaseUrl()!;
  const { path } = await params;
  const upstreamUrl = new URL(
    `${gatewayBaseUrl}/gateway/${path.join("/")}${request.nextUrl.search}`
  );
  const routeTemplate = resolveBffProxyRoute(request.method, request.nextUrl.pathname);
  const gatewayRouteTemplate = routeTemplate.replace(/^\/api\//, "/gateway/");
  const session = request.auth;

  if (session?.error === "RefreshUnavailable") {
    return NextResponse.json(
      { message: "Authentication refresh is temporarily unavailable." },
      { status: 503, headers: { "retry-after": "1" } },
    );
  }

  if (session?.error) {
    return NextResponse.json(
      { message: "Authentication session expired. Please sign in again." },
      { status: 401 }
    );
  }

  if (!session?.accessToken) {
    return NextResponse.json({ message: "Authentication is required." }, { status: 401 });
  }
  const recordId = await getRefreshTokenRecordId(request);
  if (!recordId) {
    return NextResponse.json({ message: "Authentication session is unavailable." }, { status: 401 });
  }

  const requestBody = METHODS_WITHOUT_BODY.has(request.method)
    ? undefined
    : await request.arrayBuffer();
  const proxyHeaders = buildProxyHeaders(request, session.accessToken);
  applyGatewaySessionProof(proxyHeaders, recordId, session.accessToken);
  const clientId = proxyHeaders.get("client-id") ?? undefined;

  logDevelopmentHttp("backend api request", {
    method: request.method,
    url: upstreamUrl.toString(),
    clientId,
    headers: getRequestHeadersForLog(proxyHeaders, GATEWAY_PROOF_HEADERS),
    body: requestBody ? new TextDecoder().decode(requestBody) : undefined,
  });

  const upstreamResponse = await getServerTracer().startActiveSpan(
    `gateway ${request.method.toUpperCase()} ${gatewayRouteTemplate}`,
    { kind: SpanKind.CLIENT },
    async (span) => {
      span.setAttribute("http.request.method", request.method.toUpperCase());
      span.setAttribute("url.path", upstreamUrl.pathname);
      logServerEvent(SeverityNumber.INFO, "INFO", "BFF API proxy request started", {
        "http.request.method": request.method.toUpperCase(),
        "http.route": routeTemplate,
      });

      try {
        propagation.inject(context.active(), proxyHeaders, {
          set(carrier, key, value) {
            carrier.set(key, value);
          },
        });
        const upstreamResponse = await fetch(upstreamUrl, {
          method: request.method,
          headers: proxyHeaders,
          body: requestBody,
          cache: "no-store",
          redirect: "manual",
        });
        span.setAttribute("http.response.status_code", upstreamResponse.status);
        span.setStatus({
          code: upstreamResponse.ok ? SpanStatusCode.OK : SpanStatusCode.ERROR,
        });
        logServerEvent(
          upstreamResponse.ok ? SeverityNumber.INFO : SeverityNumber.WARN,
          upstreamResponse.ok ? "INFO" : "WARN",
          "BFF API proxy request completed",
          {
            "http.request.method": request.method.toUpperCase(),
            "http.route": routeTemplate,
            "http.response.status_code": upstreamResponse.status,
          },
        );
        return upstreamResponse;
      } catch (error) {
        span.recordException(error instanceof Error ? error : new Error(String(error)));
        span.setStatus({ code: SpanStatusCode.ERROR });
        logServerEvent(SeverityNumber.ERROR, "ERROR", "BFF API proxy request failed", {
          "http.request.method": request.method.toUpperCase(),
          "http.route": routeTemplate,
          "error.type": error instanceof Error ? error.name : "UnknownError",
        });
        throw error;
      } finally {
        span.end();
      }
    },
  );

  logDevelopmentHttp("backend api response", {
    method: request.method,
    url: upstreamUrl.toString(),
    status: upstreamResponse.status,
    statusText: upstreamResponse.statusText,
    headers: getRequestHeadersForLog(upstreamResponse.headers),
    body: await getResponseBodyForLog(upstreamResponse),
  });

  return new Response(upstreamResponse.body, {
    status: upstreamResponse.status,
    headers: upstreamResponse.headers,
  });
}

async function authenticatedProxyRequest(request: NextRequest, routeContext: RouteContext) {
  const handler = await auth(proxyRequestWithSession);
  return handler(request, routeContext);
}

async function proxyRequest(request: NextRequest, routeContext: RouteContext) {
  const parentContext = resolveRequestTraceContext({
    traceparent: request.headers.get("traceparent") ?? undefined,
    tracestate: request.headers.get("tracestate") ?? undefined,
  });

  return context.with(parentContext, () => proxyRequestInContext(request, routeContext));
}

export async function GET(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}

export async function POST(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}

export async function PUT(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}

export async function PATCH(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}

export async function DELETE(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}

export async function OPTIONS(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}

export async function HEAD(request: NextRequest, context: RouteContext) {
  return proxyRequest(request, context);
}
