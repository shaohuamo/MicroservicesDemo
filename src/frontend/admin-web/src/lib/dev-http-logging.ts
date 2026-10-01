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

  console.info(`[dev-http] ${message}`, redactLogValue(data));
}

function redactLogValue(value: unknown, key = ""): unknown {
  if (/authorization|cookie|token|secret|signature|proof|session.?id|record.?id|body/i.test(key)) {
    return "[REDACTED]";
  }
  if (Array.isArray(value)) return value.map((entry) => redactLogValue(entry));
  if (value && typeof value === "object") {
    return Object.fromEntries(Object.entries(value).map(([name, entry]) => [name, redactLogValue(entry, name)]));
  }
  if (typeof value === "string") {
    return value.replace(/Bearer\s+[^\s]+/gi, "Bearer [REDACTED]");
  }
  return value;
}

export function getHeadersForLog(headers: HeadersInit | undefined) {
  if (!headers) {
    return {};
  }

  return redactLogValue(Object.fromEntries(new Headers(headers).entries())) as Record<string, unknown>;
}

export function getRequestHeadersForLog(headers: Headers, omittedHeaders: readonly string[] = []) {
  const safeHeaders = new Headers(headers);
  for (const name of omittedHeaders) safeHeaders.delete(name);
  if (safeHeaders.has("idempotency-key")) {
    safeHeaders.set("idempotency-key", "[REDACTED]");
  }
  return redactLogValue(Object.fromEntries(safeHeaders.entries())) as Record<string, unknown>;
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
