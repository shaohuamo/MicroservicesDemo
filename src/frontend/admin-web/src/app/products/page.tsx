"use client";

import { useState } from "react";
import { useProducts } from "@/hooks/use-products";
import { formatCurrency } from "@/lib/utils";
import { ProductFormDialog } from "@/components/products/product-form-dialog";
import { DeleteConfirmDialog } from "@/components/products/delete-confirm-dialog";
import type { ProductResponse } from "@/types/product";

export default function ProductsPage() {
  const { data: products, isLoading, error } = useProducts();
  const [formOpen, setFormOpen] = useState(false);
  const [formSessionId, setFormSessionId] = useState(0);
  const [editProduct, setEditProduct] = useState<ProductResponse | undefined>();
  const [deleteProduct, setDeleteProduct] = useState<ProductResponse | null>(
    null
  );

  function handleAdd() {
    setEditProduct(undefined);
    setFormSessionId((prev) => prev + 1);
    setFormOpen(true);
  }

  function handleEdit(product: ProductResponse) {
    setEditProduct(product);
    setFormSessionId((prev) => prev + 1);
    setFormOpen(true);
  }

  function handleFormClose(open: boolean) {
    setFormOpen(open);
    if (!open) setEditProduct(undefined);
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold">Products</h1>
          <p className="text-sm text-gray-500 mt-1">
            Manage your product catalog
          </p>
        </div>
        <button
          onClick={handleAdd}
          className="px-4 py-2 text-sm rounded-md bg-blue-600 text-white hover:bg-blue-700"
        >
          + Add Product
        </button>
      </div>

      {isLoading && (
        <div className="space-y-3">
          {[...Array(5)].map((_, i) => (
            <div
              key={i}
              className="h-12 bg-gray-200 dark:bg-gray-800 rounded animate-pulse"
            />
          ))}
        </div>
      )}

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 dark:bg-red-900/20 p-4">
          <p className="text-sm text-red-600 dark:text-red-400">
            Failed to load products. Make sure the backend is running.
          </p>
        </div>
      )}

      {products && products.length === 0 && (
        <div className="text-center py-12 text-gray-500">
          <p className="text-lg">No products yet</p>
          <p className="text-sm mt-1">Click &quot;Add Product&quot; to create one.</p>
        </div>
      )}

      {products && products.length > 0 && (
        <div className="border rounded-lg overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 dark:bg-gray-800">
              <tr>
                <th className="text-left px-4 py-3 font-medium">
                  Product Name
                </th>
                <th className="text-right px-4 py-3 font-medium">
                  Unit Price
                </th>
                <th className="text-right px-4 py-3 font-medium">
                  Qty In Stock
                </th>
                <th className="text-right px-4 py-3 font-medium">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {products.map((product) => (
                <tr
                  key={product.productId}
                  className="hover:bg-gray-50 dark:hover:bg-gray-800/50"
                >
                  <td className="px-4 py-3">{product.productName ?? "-"}</td>
                  <td className="px-4 py-3 text-right">
                    {formatCurrency(product.unitPrice)}
                  </td>
                  <td className="px-4 py-3 text-right">
                    {product.quantityInStock ?? "-"}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <button
                      onClick={() => handleEdit(product)}
                      className="text-blue-600 hover:text-blue-800 text-sm mr-3"
                    >
                      Edit
                    </button>
                    <button
                      onClick={() => setDeleteProduct(product)}
                      className="text-red-600 hover:text-red-800 text-sm"
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <ProductFormDialog
        key={formSessionId}
        open={formOpen}
        onOpenChange={handleFormClose}
        product={editProduct}
      />

      <DeleteConfirmDialog
        open={!!deleteProduct}
        onOpenChange={(open) => {
          if (!open) setDeleteProduct(null);
        }}
        product={deleteProduct}
      />
    </div>
  );
}
