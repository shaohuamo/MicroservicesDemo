"use client";

import { useRef } from "react";
import axios from "axios";

/** Keep the same key while the result of an unchanged request is uncertain. */
export function useProductOperationKey() {
  const operation = useRef<{ key: string; fingerprint: string } | null>(null);

  function getKey(request: unknown): string {
    const fingerprint = JSON.stringify(request);
    if (operation.current?.fingerprint !== fingerprint) {
      operation.current = { key: crypto.randomUUID(), fingerprint };
    }
    return operation.current.key;
  }

  function reset() {
    operation.current = null;
  }

  function handleError(error: unknown) {
    if (axios.isAxiosError(error)) {
      const status = error.response?.status;
      if (status !== undefined && status >= 400 && status < 500) reset();
    }
  }

  return { getKey, reset, handleError };
}
