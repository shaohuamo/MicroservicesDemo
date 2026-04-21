"use client";

import { useState } from "react";
import type { ProductResponse, ProductAddRequest, ProductUpdateRequest } from "@/types/product";
import { useAddProduct, useUpdateProduct } from "@/hooks/use-products";

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

  const [productName, setProductName] = useState(() => product?.productName ?? "");
  const [unitPrice, setUnitPrice] = useState(() => product?.unitPrice?.toString() ?? "");
  const [quantityInStock, setQuantityInStock] = useState(
    () => product?.quantityInStock?.toString() ?? ""
  );
  const [errors, setErrors] = useState<Record<string, string>>({});

  function validate(): boolean {
    const newErrors: Record<string, string> = {};
    if (!productName.trim()) {
      newErrors.productName = "Product Name can't be blank";
    }
    const price = parseFloat(unitPrice);
    if (isNaN(price) || price < 0) {
      newErrors.unitPrice = "Unit Price should be 0 or greater";
    }
    const qty = parseInt(quantityInStock, 10);
    if (isNaN(qty) || qty < 0) {
      newErrors.quantityInStock = "Quantity In Stock should be 0 or greater";
    }
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!validate()) return;

    try {
      if (isEditing) {
        const request: ProductUpdateRequest = {
          productId: product!.productId,
          productName: productName.trim(),
          unitPrice: parseFloat(unitPrice),
          quantityInStock: parseInt(quantityInStock, 10),
        };
        await updateMutation.mutateAsync(request);
      } else {
        const request: ProductAddRequest = {
          productName: productName.trim(),
          unitPrice: parseFloat(unitPrice),
          quantityInStock: parseInt(quantityInStock, 10),
        };
        await addMutation.mutateAsync(request);
      }
      onOpenChange(false);
    } catch {
      // mutation error state is rendered below
    }
  }

  const isPending = addMutation.isPending || updateMutation.isPending;

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-4 py-6 sm:px-6">
      <div
        className="dialog-backdrop fixed inset-0"
        onClick={() => onOpenChange(false)}
      />
      <div className="dialog-card relative w-full max-w-2xl rounded-[2rem] p-6 sm:p-8">
        <div className="flex flex-col gap-3 border-b border-[var(--border)] pb-5">
          <div className="kicker">Product Editor</div>
          <h2 className="font-display text-3xl font-semibold text-[var(--text)]">
            {isEditing ? "Edit Product" : "Add Product"}
          </h2>
          <p className="text-sm leading-6 text-[var(--muted)] sm:text-base">
            Adjust product identity, pricing, and stock counts from a single control surface.
          </p>
        </div>
        <form onSubmit={handleSubmit} className="mt-6 flex flex-col gap-5">
          <div className="grid gap-5 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <label className="mb-2 block text-sm font-medium text-[var(--text)]">
              Product Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={productName}
                onChange={(e) => setProductName(e.target.value)}
                className="editorial-field"
                placeholder="Enter product name"
              />
              {errors.productName && (
                <p className="mt-2 text-xs text-[var(--danger)]">{errors.productName}</p>
              )}
            </div>
            <div>
              <label className="mb-2 block text-sm font-medium text-[var(--text)]">
              Unit Price ($)
              </label>
              <input
                type="number"
                step="0.01"
                min="0"
                value={unitPrice}
                onChange={(e) => setUnitPrice(e.target.value)}
                className="editorial-field"
                placeholder="0.00"
              />
              {errors.unitPrice && (
                <p className="mt-2 text-xs text-[var(--danger)]">{errors.unitPrice}</p>
              )}
            </div>
            <div>
              <label className="mb-2 block text-sm font-medium text-[var(--text)]">
              Quantity In Stock
              </label>
              <input
                type="number"
                step="1"
                min="0"
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
            <p className="rounded-2xl border border-[rgba(232,137,110,0.28)] bg-[var(--danger-soft)] px-4 py-3 text-sm text-[var(--danger)]">
              {(addMutation.error || updateMutation.error)?.message ?? "Failed to save product. Please try again."}
            </p>
          )}
          <div className="mt-2 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
            <button
              type="button"
              onClick={() => onOpenChange(false)}
              className="editorial-button-ghost"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="editorial-button"
            >
              {isPending ? "Saving..." : isEditing ? "Update" : "Add"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
