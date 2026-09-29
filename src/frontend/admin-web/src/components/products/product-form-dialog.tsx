"use client";

import { useRef, useState } from "react";
import axios from "axios";
import type { ProductResponse, ProductAddRequest, ProductUpdateRequest } from "@/types/product";
import { useAddProduct, useUpdateProduct } from "@/hooks/use-products";
import { useT } from "@/lib/i18n/provider";
import { getApiErrorCode } from "@/lib/api/http-client";
import type { TranslationKey } from "@/lib/i18n/dictionaries";
import { useQueryClient } from "@tanstack/react-query";

interface ProductFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  product?: ProductResponse;
}

export function ProductFormDialog({
  open,
  onOpenChange,
  product,
}: ProductFormDialogProps) {
  const isEditing = !!product;
  const addMutation = useAddProduct();
  const updateMutation = useUpdateProduct();
  const t = useT();
  const queryClient = useQueryClient();

  const [displayName, setDisplayName] = useState(() => product?.displayName ?? "");
  const [unitPrice, setUnitPrice] = useState(() => product?.unitPrice?.toString() ?? "");
  const [quantityInStock, setQuantityInStock] = useState(
    () => product?.quantityInStock?.toString() ?? ""
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [refreshOnClose, setRefreshOnClose] = useState(false);
  const addOperationRef = useRef<{ key: string; fingerprint: string } | null>(null);
  const submitInFlightRef = useRef(false);

  function validate(): boolean {
    const newErrors: Record<string, string> = {};
    if (!displayName.trim()) {
      newErrors.displayName = t("productForm.nameRequired");
    } else if (!/^(?!.* {2})[\p{L}\p{N}](?:[\p{L}\p{N} .'’&()+/_-]*[\p{L}\p{N}])?$/u.test(displayName.trim())) {
      newErrors.displayName = t("productForm.nameInvalid");
    }
    const price = parseFloat(unitPrice);
    if (isNaN(price) || price < 0.01 || price > 99999999.99) {
      newErrors.unitPrice = t("productForm.priceInvalid");
    }
    const qty = parseInt(quantityInStock, 10);
    if (isNaN(qty) || qty < 0 || qty > 1000000) {
      newErrors.quantityInStock = t("productForm.quantityInvalid");
    }
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (submitInFlightRef.current) return;
    if (!validate()) return;

    submitInFlightRef.current = true;

    try {
      if (isEditing) {
        const request: ProductUpdateRequest = {
          productId: product!.productId,
          displayName: displayName.trim(),
          unitPrice: parseFloat(unitPrice),
          quantityInStock: parseInt(quantityInStock, 10),
          version: product!.version,
        };
        await updateMutation.mutateAsync(request);
      } else {
        const request: ProductAddRequest = {
          displayName: displayName.trim(),
          unitPrice: parseFloat(unitPrice),
          quantityInStock: parseInt(quantityInStock, 10),
        };
        const fingerprint = JSON.stringify(request);
        if (addOperationRef.current?.fingerprint !== fingerprint) {
          addOperationRef.current = {
            key: crypto.randomUUID(),
            fingerprint,
          };
        }
        await addMutation.mutateAsync({
          request,
          idempotencyKey: addOperationRef.current.key,
        });
        addOperationRef.current = null;
      }
      handleOpenChange(false, true);
    } catch (error) {
      const errorCode = getApiErrorCode(error);
      if (!isEditing && axios.isAxiosError(error)) {
        const status = error.response?.status;
        if (status !== undefined && status >= 400 && status < 500) {
          addOperationRef.current = null;
        }
      }
      if (isEditing && (errorCode === "product.not_found" || errorCode === "product.concurrency_conflict")) {
        setRefreshOnClose(true);
      }
    } finally {
      submitInFlightRef.current = false;
    }
  }

  function handleOpenChange(nextOpen: boolean, submittedSuccessfully = false) {
    if (!nextOpen && !isEditing) {
      addOperationRef.current = null;
      submitInFlightRef.current = false;
      if (!submittedSuccessfully) {
        void queryClient.invalidateQueries({ queryKey: ["products"] });
      }
    }
    if (!nextOpen && refreshOnClose) {
      setRefreshOnClose(false);
      void queryClient.invalidateQueries({ queryKey: ["products"] });
    }
    onOpenChange(nextOpen);
  }

  const isPending = addMutation.isPending || updateMutation.isPending;
  const mutationError = addMutation.error || updateMutation.error;
  const errorKeyByCode: Record<string, TranslationKey> = {
    "product.not_found": "productError.notFound",
    "product.already_exists": "productError.alreadyExists",
    "product.concurrency_conflict": "productError.concurrencyConflict",
    "product.database_unavailable": "productError.databaseUnavailable",
    "product.persistence_failed": "productError.persistenceFailed",
    internal_error: "productError.generic",
  };
  const errorKey = mutationError
    ? errorKeyByCode[getApiErrorCode(mutationError) ?? ""]
    : undefined;

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-4 py-6 sm:px-6">
      <div
        className="dialog-backdrop fixed inset-0"
        onClick={() => handleOpenChange(false)}
      />
      <div className="dialog-card relative w-full max-w-2xl rounded-[2rem] p-6 sm:p-8">
        <div className="flex flex-col gap-3 border-b border-[var(--border)] pb-5">
          <div className="flex items-center gap-3">
            <div className="label-chip">{t("productForm.editor")}</div>
            <span className="soft-badge">{isEditing ? t("productForm.updateRecord") : t("productForm.createRecord")}</span>
          </div>
          <h2 className="font-display text-4xl font-semibold text-[var(--text)]">
            {isEditing ? t("productForm.editTitle") : t("productForm.addTitle")}
          </h2>
          <p className="text-base leading-7 text-[var(--muted)]">
            {t("productForm.description")}
          </p>
        </div>
        <form onSubmit={handleSubmit} className="mt-6 flex flex-col gap-5">
          <div className="grid gap-5 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <label className="mb-2 block text-base font-medium text-[var(--text)]">
              {t("productForm.name")} <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                className="editorial-field"
                placeholder={t("productForm.namePlaceholder")}
              />
              {errors.displayName && (
                <p
                  role="alert"
                  className="mt-3 rounded-xl border border-[rgba(190,45,45,0.3)] bg-[var(--danger-soft)] px-4 py-3 text-sm font-medium leading-6 text-[var(--danger)]"
                >
                  {errors.displayName}
                </p>
              )}
            </div>
            <div>
              <label className="mb-2 block text-base font-medium text-[var(--text)]">
              {t("productForm.price")}
              </label>
              <input
                type="number"
                step="0.01"
                min="0.01"
                max="99999999.99"
                value={unitPrice}
                onChange={(e) => setUnitPrice(e.target.value)}
                className="editorial-field"
                placeholder="0.00"
              />
              {errors.unitPrice && (
                <p className="mt-2 text-sm font-medium text-[var(--danger)]">{errors.unitPrice}</p>
              )}
            </div>
            <div>
              <label className="mb-2 block text-base font-medium text-[var(--text)]">
              {t("productForm.quantity")}
              </label>
              <input
                type="number"
                step="1"
                min="0"
                max="1000000"
                value={quantityInStock}
                onChange={(e) => setQuantityInStock(e.target.value)}
                className="editorial-field"
                placeholder="0"
              />
              {errors.quantityInStock && (
                <p className="mt-2 text-xs text-[var(--danger)]">
                  {errors.quantityInStock}
                </p>
              )}
            </div>
          </div>
          {(addMutation.isError || updateMutation.isError) && (
            <p className="rounded-2xl border border-[rgba(232,137,110,0.28)] bg-[var(--danger-soft)] px-4 py-3 text-base leading-6 text-[var(--danger)]">
              {errorKey ? t(errorKey) : t("productForm.saveFailed")}
            </p>
          )}
          <div className="mt-2 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
            <button
              type="button"
              onClick={() => handleOpenChange(false)}
              className="editorial-button-ghost"
            >
              {t("productForm.cancel")}
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="editorial-button"
            >
              {isPending ? t("productForm.saving") : isEditing ? t("productForm.update") : t("productForm.add")}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
