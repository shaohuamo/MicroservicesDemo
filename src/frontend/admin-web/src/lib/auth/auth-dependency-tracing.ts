import { SpanKind, SpanStatusCode } from "@opentelemetry/api";
import { getAuthTracer } from "@/lib/notifications/server-otel";

type Dependency = "redis" | "postgresql";

export function traceAuthDependency<T>(
  system: Dependency,
  command: string,
  target: string | undefined,
  operation: () => Promise<T>,
): Promise<T> {
  const attributes: Record<string, string> = {
    "db.system.name": system,
    "db.operation.name": command,
  };
  if (system === "postgresql" && target) {
    attributes["db.collection.name"] = target;
  }

  return getAuthTracer().startActiveSpan(
    `${system} ${command}${target ? ` ${target}` : ""}`,
    { kind: SpanKind.CLIENT, attributes },
    async (span) => {
      try {
        const result = await operation();
        span.setStatus({ code: SpanStatusCode.OK });
        return result;
      } catch (error) {
        // Driver errors can include query text or values. Keep only the error type.
        span.setAttribute("error.type", error instanceof Error ? error.name : "UnknownError");
        span.setStatus({ code: SpanStatusCode.ERROR });
        throw error;
      } finally {
        span.end();
      }
    },
  );
}
