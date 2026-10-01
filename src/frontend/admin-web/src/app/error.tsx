"use client";

import { ServiceUnavailablePage } from "@/components/common/service-unavailable-page";

export default function Error({
  unstable_retry,
}: {
  error: Error & { digest?: string };
  unstable_retry: () => void;
}) {
  return <ServiceUnavailablePage onRetry={unstable_retry} />;
}
