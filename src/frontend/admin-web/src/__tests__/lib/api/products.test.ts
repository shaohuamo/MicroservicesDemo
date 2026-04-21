import { describe, it, expect, vi, beforeEach } from "vitest";
import type { ProductAddRequest, ProductUpdateRequest } from "@/types/product";

vi.mock("@/lib/api/http-client", () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

import { api } from "@/lib/api/http-client";
import {
  getProducts,
  getProductById,
  addProduct,
  updateProduct,
  deleteProduct,
} from "@/lib/api/products";

beforeEach(() => {
  vi.clearAllMocks();
});

describe("getProducts", () => {
  it("returns product list from GET /products", async () => {
    const products = [
      { productId: "1", productName: "Widget", unitPrice: 9.99, quantityInStock: 10 },
    ];
    vi.mocked(api.get).mockResolvedValue({ data: products });

    const result = await getProducts();
    expect(result).toEqual(products);
    expect(api.get).toHaveBeenCalledWith("/products");
  });
});

describe("getProductById", () => {
  it("calls correct URL with product id", async () => {
    const product = { productId: "abc-123", productName: "Gadget", unitPrice: 5.0, quantityInStock: 3 };
    vi.mocked(api.get).mockResolvedValue({ data: product });

    const result = await getProductById("abc-123");
    expect(result).toEqual(product);
    expect(api.get).toHaveBeenCalledWith("/products/search/product-id/abc-123");
  });
});

describe("addProduct", () => {
  it("POSTs product and returns response", async () => {
    const request: ProductAddRequest = { productName: "New", unitPrice: 1.5, quantityInStock: 100 };
    const response = { productId: "new-id", ...request };
    vi.mocked(api.post).mockResolvedValue({ data: response });

    const result = await addProduct(request);
    expect(result).toEqual(response);
    expect(api.post).toHaveBeenCalledWith("/products", request);
  });
});

describe("updateProduct", () => {
  it("PUTs product and returns response", async () => {
    const request: ProductUpdateRequest = { productId: "1", productName: "Updated", unitPrice: 2.0, quantityInStock: 50 };
    vi.mocked(api.put).mockResolvedValue({ data: request });

    const result = await updateProduct(request);
    expect(result).toEqual(request);
    expect(api.put).toHaveBeenCalledWith("/products", request);
  });
});

describe("deleteProduct", () => {
  it("DELETEs product by id", async () => {
    vi.mocked(api.delete).mockResolvedValue({ data: true });

    const result = await deleteProduct("del-id");
    expect(result).toBe(true);
    expect(api.delete).toHaveBeenCalledWith("/products/del-id");
  });
});
