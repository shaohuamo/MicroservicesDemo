export async function register() {
  if (process.env.NEXT_RUNTIME === "nodejs") {
    const { initializeServerTelemetry } = await import(
      "@/lib/notifications/server-otel"
    );
    initializeServerTelemetry();
  }
}
