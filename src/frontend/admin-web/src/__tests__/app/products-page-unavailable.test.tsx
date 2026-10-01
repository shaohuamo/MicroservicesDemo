import { afterEach, describe, expect, it, vi } from "vitest";
import { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from "axios";
import { fireEvent, render, screen } from "@testing-library/react";
import ProductsPage from "@/app/products/page";
import { useProducts } from "@/hooks/use-products";
import { I18nProvider } from "@/lib/i18n/provider";

vi.mock("@/hooks/use-products", () => ({ useProducts: vi.fn() }));
vi.mock("@/components/products/product-form-dialog", () => ({
  ProductFormDialog: () => null,
}));
vi.mock("@/components/products/delete-confirm-dialog", () => ({
  DeleteConfirmDialog: () => null,
}));

function productRequestError(status: number) {
  const config = { headers: new AxiosHeaders() } as InternalAxiosRequestConfig;
  return new AxiosError("Product request failed", undefined, config, null, {
    status,
    statusText: "",
    data: null,
    headers: new AxiosHeaders(),
    config,
  });
}

function renderFailedProducts(status: number) {
  const refetch = vi.fn();
  vi.mocked(useProducts).mockReturnValue({
    data: undefined,
    isLoading: false,
    isFetching: false,
    error: productRequestError(status),
    refetch,
  } as unknown as ReturnType<typeof useProducts>);
  render(
    <I18nProvider locale="zh-CN">
      <ProductsPage />
    </I18nProvider>,
  );
  return refetch;
}

describe("products page service failures", () => {
  afterEach(() => vi.clearAllMocks());

  it("replaces the dashboard with a generic page on a database 503", () => {
    const refetch = renderFailedProducts(503);

    expect(screen.getByRole("heading", { name: "服务暂不可用" })).toBeInTheDocument();
    expect(screen.queryByText("产品加载失败。")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "添加产品" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "重试" }));
    expect(refetch).toHaveBeenCalledOnce();
  });

  it("keeps the product-specific state for a non-service error", () => {
    renderFailedProducts(404);

    expect(screen.getByText("产品加载失败。")).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "服务暂不可用" })).not.toBeInTheDocument();
  });
});
