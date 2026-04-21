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

  const totalProducts = products?.length ?? 0;
  const totalUnits =
    products?.reduce((sum, product) => sum + (product.quantityInStock ?? 0), 0) ?? 0;
  const totalInventoryValue =
    products?.reduce(
      (sum, product) =>
        sum + (product.unitPrice ?? 0) * (product.quantityInStock ?? 0),
      0
    ) ?? 0;
  const averagePrice = totalProducts > 0
    ? (products?.reduce((sum, product) => sum + (product.unitPrice ?? 0), 0) ?? 0) /
      totalProducts
    : 0;

  const stats = [
    { label: "Products", value: totalProducts.toString().padStart(2, "0"), detail: "Tracked SKUs" },
    { label: "Units in Stock", value: totalUnits.toLocaleString(), detail: "Live inventory volume" },
    { label: "Inventory Value", value: formatCurrency(totalInventoryValue), detail: "Combined sell-through value" },
    { label: "Average Price", value: formatCurrency(averagePrice), detail: "Per product benchmark" },
  ];

  return (
    <div className="space-y-6 lg:space-y-8">
      <section className="surface-panel relative overflow-hidden rounded-[2rem] px-5 py-6 sm:px-7 sm:py-8 lg:px-10 lg:py-10">
        <div className="absolute inset-y-0 right-0 hidden w-1/3 bg-[radial-gradient(circle_at_top_right,rgba(243,180,107,0.18),transparent_58%)] lg:block" />
        <div className="relative z-10 flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
          <div className="max-w-3xl">
            <div className="label-chip">Products Control Room</div>
            <h1 className="font-display mt-5 text-4xl font-semibold leading-none text-[var(--text)] sm:text-5xl lg:text-[clamp(3.5rem,6vw,5.5rem)]">
              Products
            </h1>
            <p className="mt-4 max-w-2xl text-sm leading-7 text-[var(--muted)] sm:text-base">
              Manage your product catalog with a faster operational view of pricing, stock depth, and inventory value.
            </p>
          </div>
          <button onClick={handleAdd} className="editorial-button self-start lg:self-auto">
            <span className="text-lg leading-none">+</span>
            <span>Add Product</span>
          </button>
        </div>
      </section>

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {stats.map((stat) => (
          <article key={stat.label} className="stat-card rounded-[1.6rem] p-5 sm:p-6">
            <div className="kicker">{stat.label}</div>
            <div className="font-display mt-4 text-3xl font-semibold text-[var(--text)] sm:text-[2.35rem]">
              {stat.value}
            </div>
            <p className="mt-2 text-sm leading-6 text-[var(--muted)]">{stat.detail}</p>
          </article>
        ))}
      </section>

      {isLoading && (
        <section className="space-y-6">
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {[...Array(4)].map((_, index) => (
              <div key={index} className="stat-card rounded-[1.6rem] p-5 sm:p-6">
                <div className="skeleton-block h-3 w-24 rounded-full" />
                <div className="skeleton-block mt-4 h-10 w-32 rounded-2xl" />
                <div className="skeleton-block mt-4 h-3 w-40 rounded-full" />
              </div>
            ))}
          </div>
          <div className="surface-panel rounded-[1.8rem] p-4 sm:p-6">
            <div className="skeleton-block h-5 w-40 rounded-full" />
            <div className="mt-6 space-y-3">
              {[...Array(5)].map((_, index) => (
                <div key={index} className="skeleton-block h-16 rounded-[1.1rem]" />
              ))}
            </div>
          </div>
        </section>
      )}

      {error && (
        <section className="state-panel rounded-[1.8rem] border-[color:var(--danger-soft)] p-6 sm:p-8">
          <div className="kicker text-[var(--danger)]">Service Error</div>
          <h2 className="font-display mt-3 text-3xl font-semibold text-[var(--text)]">
            Failed to load products.
          </h2>
          <p className="mt-4 max-w-2xl text-sm leading-7 text-[var(--muted)] sm:text-base">
            Make sure the backend is running and reachable through the gateway, then refresh the page.
          </p>
        </section>
      )}

      {products && products.length === 0 && (
        <section className="state-panel rounded-[1.8rem] p-8 text-center sm:p-12">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-[1.35rem] border border-[var(--border-strong)] bg-[var(--accent-soft)] text-[var(--accent-strong)]">
            <svg viewBox="0 0 24 24" fill="none" className="h-8 w-8" aria-hidden="true">
              <path d="M4 7.5 12 3l8 4.5M4 7.5v9L12 21m-8-13.5L12 12m8-4.5L12 12m0 9v-9" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </div>
          <h2 className="font-display mt-6 text-3xl font-semibold text-[var(--text)]">No products yet</h2>
          <p className="mx-auto mt-4 max-w-xl text-sm leading-7 text-[var(--muted)] sm:text-base">
            The catalog is empty. Click &quot;Add Product&quot; to create one and start tracking inventory.
          </p>
        </section>
      )}

      {products && products.length > 0 && (
        <section className="surface-panel overflow-hidden rounded-[1.8rem]">
          <div className="flex flex-col gap-3 border-b border-[var(--border)] px-5 py-5 sm:px-6 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <div className="kicker">Catalog Matrix</div>
              <h2 className="font-display mt-2 text-2xl font-semibold text-[var(--text)] sm:text-3xl">
                Active product inventory
              </h2>
            </div>
            <p className="max-w-xl text-sm leading-6 text-[var(--muted)]">
              Prices, stock levels, and destructive actions stay available here without leaving the control surface.
            </p>
          </div>
          <div className="table-scroll">
            <table className="data-table w-full text-sm">
            <thead>
              <tr>
                <th>Product Name</th>
                <th className="text-right">Unit Price</th>
                <th className="text-right">Qty In Stock</th>
                <th className="text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {products.map((product) => (
                <tr key={product.productId}>
                  <td>
                    <div className="flex items-center gap-3">
                      <div className="flex h-11 w-11 items-center justify-center rounded-2xl border border-[var(--border)] bg-[rgba(255,255,255,0.03)] text-[var(--accent-strong)]">
                        <svg viewBox="0 0 24 24" fill="none" className="h-5 w-5" aria-hidden="true">
                          <path d="M4 7.5 12 3l8 4.5M4 7.5v9L12 21m-8-13.5L12 12m8-4.5L12 12m0 9v-9" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                      </div>
                      <div>
                        <div className="font-medium text-[var(--text)]">{product.productName ?? "-"}</div>
                        <div className="mt-1 text-xs uppercase tracking-[0.18em] text-[var(--muted)]">SKU record</div>
                      </div>
                    </div>
                  </td>
                  <td className="text-right font-medium text-[var(--text)]">
                    {formatCurrency(product.unitPrice)}
                  </td>
                  <td className="text-right text-[var(--text)]">
                    {product.quantityInStock ?? "-"}
                  </td>
                  <td className="text-right">
                    <button
                      onClick={() => handleEdit(product)}
                      className="mr-2 rounded-full border border-[var(--border-strong)] px-3 py-2 text-sm font-medium text-[var(--text)] hover:bg-[rgba(255,255,255,0.05)]"
                    >
                      Edit
                    </button>
                    <button
                      onClick={() => setDeleteProduct(product)}
                      className="rounded-full border border-[rgba(232,137,110,0.3)] px-3 py-2 text-sm font-medium text-[var(--danger)] hover:bg-[rgba(232,137,110,0.08)]"
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        </section>
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
