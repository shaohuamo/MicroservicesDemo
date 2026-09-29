"use client";

import { DEFAULT_LOCALE, normalizeLocale } from "@/lib/i18n/config";
import { translate } from "@/lib/i18n/dictionaries";

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const locale = typeof navigator === "undefined"
    ? DEFAULT_LOCALE
    : normalizeLocale(navigator.language) ?? DEFAULT_LOCALE;

  return (
    <html lang={locale}>
      <body className="min-h-screen flex items-center justify-center bg-gray-50">
        <div className="rounded-lg border border-red-200 bg-red-50 p-6 max-w-md text-center">
          <h2 className="text-lg font-semibold text-red-700 mb-2">
            {translate(locale, "error.title")}
          </h2>
          <p className="text-sm text-red-600 mb-4">
            {translate(locale, "error.critical")}
          </p>
          <button
            onClick={reset}
            className="px-4 py-2 text-sm rounded-md bg-red-600 text-white hover:bg-red-700"
          >
            {translate(locale, "error.retry")}
          </button>
        </div>
      </body>
    </html>
  );
}
