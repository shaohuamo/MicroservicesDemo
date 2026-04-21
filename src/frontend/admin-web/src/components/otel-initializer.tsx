"use client";

import { useEffect } from "react";

export function OtelInitializer() {
  useEffect(() => {
    import("@/lib/otel").then(({ initOpenTelemetry }) => {
      initOpenTelemetry();
    });
  }, []);

  return null;
}
