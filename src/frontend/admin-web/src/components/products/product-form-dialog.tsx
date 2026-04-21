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
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div
        className="fixed inset-0 bg-black/50"
        onClick={() => onOpenChange(false)}
      />
      <div className="relative bg-white dark:bg-gray-900 rounded-lg shadow-lg border p-6 w-full max-w-md">
        <h2 className="text-lg font-semibold mb-4">
          {isEditing ? "Edit Product" : "Add Product"}
        </h2>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div>
            <label className="block text-sm font-medium mb-1">
              Product Name <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={productName}
              onChange={(e) => setProductName(e.target.value)}
              className="w-full rounded-md border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 dark:bg-gray-800 dark:border-gray-700"
              placeholder="Enter product name"
            />
            {errors.productName && (
              <p className="text-red-500 text-xs mt-1">{errors.productName}</p>
            )}
          </div>
          <div>
            <label className="block text-sm font-medium mb-1">
              Unit Price ($)
            </label>
            <input
              type="number"
              step="0.01"
              min="0"
              value={unitPrice}
              onChange={(e) => setUnitPrice(e.target.value)}
              className="w-full rounded-md border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 dark:bg-gray-800 dark:border-gray-700"
              placeholder="0.00"
            />
            {errors.unitPrice && (
              <p className="text-red-500 text-xs mt-1">{errors.unitPrice}</p>
            )}
          </div>
          <div>
            <label className="block text-sm font-medium mb-1">
              Quantity In Stock
            </label>
            <input
              type="number"
              step="1"
              min="0"
              value={quantityInStock}
              onChange={(e) => setQuantityInStock(e.target.value)}
              className="w-full rounded-md border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 dark:bg-gray-800 dark:border-gray-700"
              placeholder="0"
            />
            {errors.quantityInStock && (
              <p className="text-red-500 text-xs mt-1">
                {errors.quantityInStock}
              </p>
            )}
          </div>
          {(addMutation.isError || updateMutation.isError) && (
            <p className="text-red-500 text-sm">
              {(addMutation.error || updateMutation.error)?.message ?? "Failed to save product. Please try again."}
            </p>
          )}
          <div className="flex justify-end gap-2 mt-2">
            <button
              type="button"
              onClick={() => onOpenChange(false)}
              className="px-4 py-2 text-sm rounded-md border hover:bg-gray-100 dark:hover:bg-gray-800"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="px-4 py-2 text-sm rounded-md bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {isPending ? "Saving..." : isEditing ? "Update" : "Add"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
