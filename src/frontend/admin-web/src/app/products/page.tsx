"use client";

import { useState } from "react";
import { useProducts } from "@/hooks/use-products";
import { formatCurrency } from "@/lib/utils";
import { ProductFormDialog } from "@/components/products/product-form-dialog";
import { DeleteConfirmDialog } from "@/components/products/delete-confirm-dialog";
import type { ProductResponse } from "@/types/product";
import { useI18n } from "@/lib/i18n/provider";
import { isServiceUnavailableError } from "@/lib/api/service-availability";
import { ServiceUnavailablePage } from "@/components/common/service-unavailable-page";

export default function ProductsPage() {
  const { data: products, isLoading, isFetching, error, refetch } = useProducts();
  const { locale, t } = useI18n();
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

  if (error && isServiceUnavailableError(error)) {
    return (
      <ServiceUnavailablePage
        onRetry={() => { void refetch(); }}
        retrying={isFetching}
      />
    );
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
    { label: t("products.stats.units"), value: totalUnits.toLocaleString(locale), detail: t("products.stats.unitsDetail") },
    { label: t("products.stats.value"), value: formatCurrency(totalInventoryValue, locale), detail: t("products.stats.valueDetail") },
    { label: t("products.stats.average"), value: formatCurrency(averagePrice, locale), detail: t("products.stats.averageDetail") },
  ];

  return (
    <div className="space-y-6 lg:space-y-8">
      <section className="surface-panel rounded-[1.75rem] px-5 py-6 sm:px-7 sm:py-8 lg:px-8 lg:py-8">
        <div className="flex flex-col gap-6 lg:flex-row lg:items-start lg:justify-between">
          <div className="max-w-3xl">
            <div className="flex flex-wrap items-center gap-3">
              <div className="label-chip">{t("products.workspace")}</div>
            </div>
            <h1 className="font-display mt-5 text-4xl font-semibold leading-none text-[var(--text)] sm:text-5xl lg:text-[4.6rem]">
              {t("products.title")}
            </h1>
          </div>
          <div className="flex w-full flex-col gap-3 sm:w-auto lg:min-w-[240px]">
            <div className="surface-panel-soft rounded-[1.2rem] px-4 py-4">
              <div className="kicker">{t("products.activeCatalog")}</div>
              <div className="mt-3 flex items-end gap-3">
                <span className="font-display text-4xl font-semibold text-[var(--text)]">
                  {totalProducts.toString().padStart(2, "0")}
                </span>
                <span className="pb-1 text-base text-[var(--muted)]">{t("products.inView")}</span>
              </div>
            </div>
            <button onClick={handleAdd} className="editorial-button self-start sm:self-stretch">
              <span className="text-lg leading-none">+</span>
              <span>{t("products.add")}</span>
            </button>
          </div>
        </div>
      </section>

      <section className="grid gap-4 md:grid-cols-3">
        {stats.map((stat) => (
          <article key={stat.label} className="stat-card rounded-[1.6rem] p-5 sm:p-6">
            <div className="kicker">{stat.label}</div>
            <div className="font-display mt-4 text-3xl font-semibold text-[var(--text)] sm:text-[2.35rem]">
              {stat.value}
            </div>
            {stat.detail && (
              <p className="mt-2 text-base leading-6 text-[var(--muted)]">{stat.detail}</p>
            )}
          </article>
        ))}
      </section>

      {isLoading && (
        <section className="space-y-6">
          <div className="grid gap-4 md:grid-cols-3">
            {[...Array(3)].map((_, index) => (
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
          <div className="kicker text-[var(--danger)]">{t("products.serviceError")}</div>
          <h2 className="font-display mt-3 text-3xl font-semibold text-[var(--text)]">
            {t("products.loadFailed")}
          </h2>
          <p className="mt-4 max-w-2xl text-base leading-7 text-[var(--muted)]">
            {t("products.loadFailedDetail")}
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
          <h2 className="font-display mt-6 text-3xl font-semibold text-[var(--text)]">{t("products.emptyTitle")}</h2>
          <p className="mx-auto mt-4 max-w-xl text-base leading-7 text-[var(--muted)]">
            {t("products.emptyDetail")}
          </p>
        </section>
      )}

      {products && products.length > 0 && (
        <section className="surface-panel overflow-hidden rounded-[1.8rem]">
          <div className="border-b border-[var(--border)] px-5 py-5 sm:px-6">
            <div>
              <h2 className="font-display text-2xl font-semibold text-[var(--text)] sm:text-3xl">
                {t("products.inventoryTitle")}
              </h2>
            </div>
          </div>
          <div className="table-scroll">
            <table className="data-table w-full text-base">
            <thead>
              <tr>
                <th>{t("products.table.name")}</th>
                <th className="text-right">{t("products.table.price")}</th>
                <th className="text-right">{t("products.table.quantity")}</th>
                <th className="table-actions-heading">{t("products.table.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {products.map((product) => (
                <tr key={product.productId}>
                  <td>
                    <div className="flex items-center gap-3">
                      <div className="flex h-11 w-11 items-center justify-center rounded-2xl border border-[var(--border)] bg-[var(--surface)] text-[var(--accent-strong)]">
                        <svg viewBox="0 0 24 24" fill="none" className="h-5 w-5" aria-hidden="true">
                          <path d="M4 7.5 12 3l8 4.5M4 7.5v9L12 21m-8-13.5L12 12m8-4.5L12 12m0 9v-9" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                      </div>
                      <div>
                        <div className="font-medium text-[var(--text)]">{product.displayName ?? "-"}</div>
                      </div>
                    </div>
                  </td>
                  <td className="text-right font-medium text-[var(--text)]">
                    {formatCurrency(product.unitPrice, locale)}
                  </td>
                  <td className="text-right text-[var(--text)]">
                    {product.quantityInStock ?? "-"}
                  </td>
                  <td className="table-actions-cell">
                    <div className="table-action-group">
                      <button
                        onClick={() => handleEdit(product)}
                        className="table-action-button"
                      >
                        {t("products.edit")}
                      </button>
                      <button
                        onClick={() => setDeleteProduct(product)}
                        className="table-action-button table-action-button-danger"
                      >
                        {t("products.delete")}
                      </button>
                    </div>
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
