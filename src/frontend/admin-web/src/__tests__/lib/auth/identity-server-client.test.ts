// @vitest-environment node

import {
  context,
  createTraceState,
  trace,
  TraceFlags,
} from "@opentelemetry/api";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import {
  fetchIdentityServerWithTrace,
  refreshIdentityServerAccessToken,
} from "@/lib/auth/identity-server-client";
import { initializeServerTelemetry } from "@/lib/notifications/server-otel";

function getTraceId(traceparent: string) {
  return traceparent.split("-")[1];
}

function getParentSpanId(traceparent: string) {
  return traceparent.split("-")[2];
}

describe("IdentityServer traced HTTP client", () => {
  beforeAll(() => {
    vi.stubEnv("IDENTITYSERVER_PUBLIC_URL", "http://identity.example:8485");
    vi.stubEnv("IDENTITYSERVER_INTERNAL_URL", "http://identityserver:8080");
    vi.stubEnv("IDENTITYSERVER_FRONTEND_CLIENT_ID", "admin-web");
    vi.stubEnv("IDENTITYSERVER_FRONTEND_CLIENT_SECRET", "client-secret");
    initializeServerTelemetry();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("rewrites to the internal endpoint and propagates a child client span", async () => {
    let capturedUrl = "";
    let capturedHeaders = new Headers();
    let activeClientSpanId = "";

    vi.stubGlobal("fetch", vi.fn(async (input: Parameters<typeof fetch>[0], init?: RequestInit) => {
      capturedUrl = typeof input === "string" ? input : input.toString();
      capturedHeaders = new Headers(init?.headers);
      activeClientSpanId = trace.getActiveSpan()?.spanContext().spanId ?? "";
      return new Response("{}", { status: 200 });
    }));

    const parent = trace.getTracer("identity-server-client.test").startSpan("parent");

    await context.with(trace.setSpan(context.active(), parent), () =>
      fetchIdentityServerWithTrace("http://identity.example:8485/connect/token?secret=never-record", {
        method: "POST",
        headers: {
          "content-type": "application/x-www-form-urlencoded",
          "x-request-id": "request-1",
          traceparent: "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01",
        },
        body: "grant_type=refresh_token&refresh_token=never-record",
      }),
    );

    parent.end();

    const traceparent = capturedHeaders.get("traceparent");
    expect(capturedUrl).toBe("http://identityserver:8080/connect/token?secret=never-record");
    expect(capturedHeaders.get("content-type")).toBe("application/x-www-form-urlencoded");
    expect(capturedHeaders.get("x-request-id")).toBe("request-1");
    expect(traceparent).not.toBeNull();
    expect(getTraceId(traceparent!)).toBe(parent.spanContext().traceId);
    expect(getParentSpanId(traceparent!)).toBe(activeClientSpanId);
    expect(traceparent).not.toContain("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
  });

  it("propagates tracestate from the incoming context", async () => {
    let capturedHeaders = new Headers();
    vi.stubGlobal("fetch", vi.fn(async (_input: Parameters<typeof fetch>[0], init?: RequestInit) => {
      capturedHeaders = new Headers(init?.headers);
      return new Response("{}", { status: 200 });
    }));

    const remoteParent = trace.wrapSpanContext({
      traceId: "0123456789abcdef0123456789abcdef",
      spanId: "0123456789abcdef",
      traceFlags: TraceFlags.SAMPLED,
      traceState: createTraceState("vendor=value"),
      isRemote: true,
    });

    await context.with(trace.setSpan(context.active(), remoteParent), () =>
      fetchIdentityServerWithTrace("http://identity.example:8485/userinfo"),
    );

    expect(capturedHeaders.get("tracestate")).toBe("vendor=value");
  });

  it("uses the traced transport for refresh-token requests without exposing credentials in headers", async () => {
    let capturedUrl = "";
    let capturedInit: RequestInit | undefined;
    vi.stubGlobal("fetch", vi.fn(async (input: Parameters<typeof fetch>[0], init?: RequestInit) => {
      capturedUrl = typeof input === "string" ? input : input.toString();
      capturedInit = init;
      return new Response(JSON.stringify({ access_token: "new-access-token", expires_in: 900 }), {
        status: 200,
        headers: { "content-type": "application/json" },
      });
    }));

    await expect(refreshIdentityServerAccessToken("refresh-token")).resolves.toMatchObject({
      access_token: "new-access-token",
    });

    const requestBody = capturedInit?.body as URLSearchParams;
    expect(capturedUrl).toBe("http://identityserver:8080/connect/token");
    expect(new Headers(capturedInit?.headers).get("traceparent")).not.toBeNull();
    expect(requestBody.get("grant_type")).toBe("refresh_token");
    expect(requestBody.get("refresh_token")).toBe("refresh-token");
    expect(requestBody.get("client_secret")).toBe("client-secret");
  });
});
