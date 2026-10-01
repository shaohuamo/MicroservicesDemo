"use client";

import { useRef, useState } from "react";
import { useProductOperationKey } from "@/hooks/use-product-operation-key";
import type { ProductResponse } from "@/types/product";
import { useDeleteProduct } from "@/hooks/use-products";
import { useT } from "@/lib/i18n/provider";
import { getApiErrorCode } from "@/lib/api/http-client";
import type { TranslationKey } from "@/lib/i18n/dictionaries";
import { useQueryClient } from "@tanstack/react-query";

interface DeleteConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  product: ProductResponse | null;
}

export function DeleteConfirmDialog({
  open,
  onOpenChange,
  product,
}: DeleteConfirmDialogProps) {
  const deleteMutation = useDeleteProduct();
  const operationKey = useProductOperationKey();
  const submitInFlight = useRef(false);
  const t = useT();
  const queryClient = useQueryClient();
  const [refreshOnClose, setRefreshOnClose] = useState(false);
  const errorKeyByCode: Record<string, TranslationKey> = {
    "product.not_found": "productError.notFound",
    "product.concurrency_conflict": "productError.concurrencyConflict",
    "product.database_unavailable": "productError.databaseUnavailable",
    "product.persistence_failed": "productError.persistenceFailed",
    internal_error: "productError.generic",
  };
  const errorKey = deleteMutation.error
    ? errorKeyByCode[getApiErrorCode(deleteMutation.error) ?? ""]
    : undefined;

  async function handleDelete() {
    if (!product || submitInFlight.current) return;
    submitInFlight.current = true;
    try {
      const request = { productId: product.productId, version: product.version };
      await deleteMutation.mutateAsync({ request, idempotencyKey: operationKey.getKey(request) });
      handleOpenChange(false);
    } catch (error) {
      operationKey.handleError(error);
      const errorCode = getApiErrorCode(error);
      if (errorCode === "product.not_found" || errorCode === "product.concurrency_conflict") {
        setRefreshOnClose(true);
      }
    } finally {
      submitInFlight.current = false;
    }
  }

  function handleOpenChange(nextOpen: boolean) {
    if (!nextOpen) {
      operationKey.reset();
      deleteMutation.reset();

      if (refreshOnClose) {
        setRefreshOnClose(false);
        void queryClient.invalidateQueries({ queryKey: ["products"] });
      }
    }
    onOpenChange(nextOpen);
  }

  if (!open || !product) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-4 py-6 sm:px-6">
      <div
        className="dialog-backdrop fixed inset-0"
        onClick={() => handleOpenChange(false)}
      />
      <div className="dialog-card relative w-full max-w-lg rounded-[2rem] p-6 sm:p-8">
        <div className="flex items-center gap-3">
          <div className="label-chip">{t("deleteDialog.label")}</div>
          <span className="soft-badge">{t("deleteDialog.badge")}</span>
        </div>
        <h2 className="font-display mt-4 text-4xl font-semibold text-[var(--text)]">{t("deleteDialog.title")}</h2>
        <p className="mt-4 text-base leading-7 text-[var(--muted)]">
          {t("deleteDialog.messagePrefix")}{" "}
          <strong>{product.displayName ?? "-"}</strong>
          {t("deleteDialog.messageSuffix")}
        </p>
        {deleteMutation.isError && (
          <p className="mt-5 rounded-2xl border border-[rgba(232,137,110,0.28)] bg-[var(--danger-soft)] px-4 py-3 text-base leading-6 text-[var(--danger)]">
            {errorKey ? t(errorKey) : t("deleteDialog.failed")}
          </p>
        )}
        <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <button
            type="button"
            onClick={() => handleOpenChange(false)}
            className="editorial-button-ghost"
          >
            {t("deleteDialog.cancel")}
          </button>
          <button
            type="button"
            onClick={handleDelete}
            disabled={deleteMutation.isPending}
            className="editorial-button-danger"
          >
            {deleteMutation.isPending ? t("deleteDialog.deleting") : t("deleteDialog.delete")}
          </button>
        </div>
      </div>
    </div>
  );
}
