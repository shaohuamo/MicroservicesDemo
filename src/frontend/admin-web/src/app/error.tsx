"use client";

export default function Error({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="flex flex-col items-center justify-center py-20">
      <div className="rounded-lg border border-red-200 bg-red-50 dark:bg-red-900/20 p-6 max-w-md text-center">
        <h2 className="text-lg font-semibold text-red-700 dark:text-red-400 mb-2">
          Something went wrong
        </h2>
        <p className="text-sm text-red-600 dark:text-red-300 mb-4">
          {error.message || "An unexpected error occurred."}
        </p>
        <button
          onClick={reset}
          className="px-4 py-2 text-sm rounded-md bg-red-600 text-white hover:bg-red-700"
        >
          Try again
        </button>
      </div>
    </div>
  );
}
