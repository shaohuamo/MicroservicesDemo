"use client";

import { useT } from "@/lib/i18n/provider";

export function ServiceUnavailablePage({
  onRetry,
  retrying = false,
}: {
  onRetry: () => void;
  retrying?: boolean;
}) {
  const t = useT();

  return (
    <section
      role="alert"
      className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-[var(--bg-elevated)] px-4 py-8"
    >
      <div className="surface-panel w-full max-w-2xl rounded-[2rem] px-6 py-12 text-center sm:px-12 sm:py-16">
        <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-[var(--accent-soft)] text-[var(--accent-strong)]">
          <svg viewBox="0 0 24 24" fill="none" className="h-8 w-8" aria-hidden="true">
            <path d="M4 8h16M4 16h16M6 5h12a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2Z" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
            <path d="M8 12h.01M12 12h.01" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
          </svg>
        </div>
        <h1 className="font-display mt-7 text-3xl font-semibold text-[var(--text)] sm:text-4xl">
          {t("error.serviceUnavailable")}
        </h1>
        <p className="mx-auto mt-4 max-w-md text-base leading-7 text-[var(--muted)]">
          {t("error.serviceUnavailableDetail")}
        </p>
        <button
          type="button"
          className="editorial-button mt-8 min-w-36"
          onClick={onRetry}
          disabled={retrying}
        >
          {retrying ? t("error.retrying") : t("error.retry")}
        </button>
      </div>
    </section>
  );
}
