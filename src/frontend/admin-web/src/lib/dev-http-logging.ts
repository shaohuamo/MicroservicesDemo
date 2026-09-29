const DEVELOPMENT_ENVIRONMENT_NAMES = new Set(["development", "dev"]);

export function isDevelopmentHttpLoggingEnabled() {
  const appEnvironment = process.env.APP_ENV?.toLowerCase();
  if (appEnvironment) {
    return DEVELOPMENT_ENVIRONMENT_NAMES.has(appEnvironment);
  }

  return process.env.NODE_ENV?.toLowerCase() === "development";
}

export function logDevelopmentHttp(message: string, data: Record<string, unknown>) {
  if (!isDevelopmentHttpLoggingEnabled()) {
    return;
  }

  console.info(`[dev-http] ${message}`, data);
}

export function getHeadersForLog(headers: HeadersInit | undefined) {
  if (!headers) {
    return {};
  }

  return Object.fromEntries(new Headers(headers).entries());
}

export function getRequestHeadersForLog(headers: Headers) {
  const safeHeaders = new Headers(headers);
  if (safeHeaders.has("idempotency-key")) {
    safeHeaders.set("idempotency-key", "[REDACTED]");
  }
  return Object.fromEntries(safeHeaders.entries());
}

export function getBodyForLog(body: BodyInit | null | undefined) {
  if (!body) {
    return undefined;
  }

  if (typeof body === "string") {
    return body;
  }

  if (body instanceof URLSearchParams) {
    return body.toString();
  }

  if (body instanceof FormData) {
    return Object.fromEntries(body.entries());
  }

  if (body instanceof Blob) {
    return `[Blob size=${body.size} type=${body.type}]`;
  }

  if (body instanceof ArrayBuffer) {
    return new TextDecoder().decode(body);
  }

  if (ArrayBuffer.isView(body)) {
    return new TextDecoder().decode(body);
  }

  return `[${body.constructor.name}]`;
}

export function getUrlForLog(input: Parameters<typeof fetch>[0]) {
  if (typeof input === "string") {
    return input;
  }

  if (input instanceof URL) {
    return input.toString();
  }

  return input.url;
}

export function getMethodForLog(input: Parameters<typeof fetch>[0], init?: Parameters<typeof fetch>[1]) {
  if (init?.method) {
    return init.method;
  }

  if (typeof input === "object" && !(input instanceof URL)) {
    return input.method;
  }

  return "GET";
}

export async function getResponseBodyForLog(response: Response) {
  try {
    return await response.clone().text();
  } catch (error) {
    return `[unavailable: ${error instanceof Error ? error.message : "unknown error"}]`;
  }
}
