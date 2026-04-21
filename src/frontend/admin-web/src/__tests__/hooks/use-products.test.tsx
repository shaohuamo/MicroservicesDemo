import { describe, it, expect, vi } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useProducts, useAddProduct, useDeleteProduct } from "@/hooks/use-products";
import * as productsApi from "@/lib/api/products";
import type { ReactNode } from "react";

vi.mock("@/lib/api/products");

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  Wrapper.displayName = "UseProductsQueryClientWrapper";
  return Wrapper;
}

describe("useProducts", () => {
  it("fetches product list", async () => {
    const products = [
      { productId: "1", productName: "A", unitPrice: 1, quantityInStock: 10 },
    ];
    vi.mocked(productsApi.getProducts).mockResolvedValue(products);

    const { result } = renderHook(() => useProducts(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(products);
  });

  it("returns error on failure", async () => {
    vi.mocked(productsApi.getProducts).mockRejectedValue(new Error("Network error"));

    const { result } = renderHook(() => useProducts(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe("Network error");
  });
});

describe("useAddProduct", () => {
  it("calls addProduct and invalidates queries", async () => {
    const newProduct = { productId: "2", productName: "B", unitPrice: 5, quantityInStock: 20 };
    vi.mocked(productsApi.addProduct).mockResolvedValue(newProduct);

    const { result } = renderHook(() => useAddProduct(), { wrapper: createWrapper() });

    await result.current.mutateAsync({ productName: "B", unitPrice: 5, quantityInStock: 20 });

    expect(productsApi.addProduct).toHaveBeenCalledWith({
      productName: "B",
      unitPrice: 5,
      quantityInStock: 20,
    });
  });
});

describe("useDeleteProduct", () => {
  it("calls deleteProduct with product id", async () => {
    vi.mocked(productsApi.deleteProduct).mockResolvedValue(true);

    const { result } = renderHook(() => useDeleteProduct(), { wrapper: createWrapper() });

    await result.current.mutateAsync("del-id");

    expect(productsApi.deleteProduct).toHaveBeenCalledWith("del-id");
  });
});
