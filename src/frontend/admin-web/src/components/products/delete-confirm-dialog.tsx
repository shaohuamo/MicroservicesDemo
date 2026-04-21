"use client";

import type { ProductResponse } from "@/types/product";
import { useDeleteProduct } from "@/hooks/use-products";

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

  async function handleDelete() {
    if (!product) return;
    try {
      await deleteMutation.mutateAsync(product.productId);
      onOpenChange(false);
    } catch {
      // mutation error state is rendered below
    }
  }

  if (!open || !product) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div
        className="fixed inset-0 bg-black/50"
        onClick={() => onOpenChange(false)}
      />
      <div className="relative bg-white dark:bg-gray-900 rounded-lg shadow-lg border p-6 w-full max-w-sm">
        <h2 className="text-lg font-semibold mb-2">Delete Product</h2>
        <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
          Are you sure you want to delete{" "}
          <strong>{product.productName}</strong>? This action cannot be undone.
        </p>
        {deleteMutation.isError && (
          <p className="text-red-500 text-sm mb-3">
            {deleteMutation.error?.message ?? "Failed to delete product. Please try again."}
          </p>
        )}
        <div className="flex justify-end gap-2">
          <button
            type="button"
            onClick={() => onOpenChange(false)}
            className="px-4 py-2 text-sm rounded-md border hover:bg-gray-100 dark:hover:bg-gray-800"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleDelete}
            disabled={deleteMutation.isPending}
            className="px-4 py-2 text-sm rounded-md bg-red-600 text-white hover:bg-red-700 disabled:opacity-50"
          >
            {deleteMutation.isPending ? "Deleting..." : "Delete"}
          </button>
        </div>
      </div>
    </div>
  );
}
