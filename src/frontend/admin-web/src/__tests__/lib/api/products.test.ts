import { describe, it, expect, vi, beforeEach } from "vitest";
import type { ProductAddOperation, ProductUpdateRequest } from "@/types/product";

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
      { productId: "1", displayName: "Widget", unitPrice: 9.99, quantityInStock: 10, version: 1 },
    ];
    vi.mocked(api.get).mockResolvedValue({ data: products });

    const result = await getProducts();
    expect(result).toEqual(products);
    expect(api.get).toHaveBeenCalledWith("/products");
  });
});

describe("getProductById", () => {
  it("calls correct URL with product id", async () => {
    const product = { productId: "abc-123", displayName: "Gadget", unitPrice: 5.0, quantityInStock: 3, version: 2 };
    vi.mocked(api.get).mockResolvedValue({ data: product });

    const result = await getProductById("abc-123");
    expect(result).toEqual(product);
    expect(api.get).toHaveBeenCalledWith("/products/search/product-id/abc-123");
  });
});

describe("addProduct", () => {
  it("POSTs product and returns response", async () => {
    const request = { displayName: "New", unitPrice: 1.5, quantityInStock: 100 };
    const operation: ProductAddOperation = {
      request,
      idempotencyKey: "7f277273-b334-47f9-8b59-d37aa3473665",
    };
    const response = { productId: "new-id", ...request, version: 1 };
    vi.mocked(api.post).mockResolvedValue({ data: response });

    const result = await addProduct(operation);
    expect(result).toEqual(response);
    expect(api.post).toHaveBeenCalledWith("/products", request, {
      headers: { "Idempotency-Key": operation.idempotencyKey },
    });
  });
});

describe("updateProduct", () => {
  it("PUTs product and returns response", async () => {
    const request: ProductUpdateRequest = { productId: "1", displayName: "Updated", unitPrice: 2.0, quantityInStock: 50, version: 3 };
    vi.mocked(api.put).mockResolvedValue({ data: request });

    const operation = { request, idempotencyKey: "7f277273-b334-47f9-8b59-d37aa3473665" };
    const result = await updateProduct(operation);
    expect(result).toEqual(request);
    expect(api.put).toHaveBeenCalledWith("/products", request, {
      headers: { "Idempotency-Key": operation.idempotencyKey },
    });
  });
});

describe("deleteProduct", () => {
  it("DELETEs product by id", async () => {
    vi.mocked(api.delete).mockResolvedValue({ data: true });

    const operation = { request: { productId: "del-id", version: 4 }, idempotencyKey: "7f277273-b334-47f9-8b59-d37aa3473665" };
    const result = await deleteProduct(operation);
    expect(result).toBe(true);
    expect(api.delete).toHaveBeenCalledWith("/products/del-id", {
      data: { version: 4 },
      headers: { "Idempotency-Key": operation.idempotencyKey },
    });
  });
});
