import type {
  ProductAddOperation,
  ProductDeleteOperation,
  ProductResponse,
  ProductUpdateOperation,
} from "@/types/product";
import { api } from "./http-client";

export async function getProducts(): Promise<ProductResponse[]> {
  const { data } = await api.get<ProductResponse[]>("/products");
  return data;
}

export async function getProductById(
  productId: string
): Promise<ProductResponse> {
  const { data } = await api.get<ProductResponse>(
    `/products/search/product-id/${productId}`
  );
  return data;
}

export async function addProduct(
  operation: ProductAddOperation
): Promise<ProductResponse> {
  const { data } = await api.post<ProductResponse>(
    "/products",
    operation.request,
    { headers: { "Idempotency-Key": operation.idempotencyKey } },
  );
  return data;
}

export async function updateProduct(
  operation: ProductUpdateOperation
): Promise<ProductResponse> {
  const { data } = await api.put<ProductResponse>("/products", operation.request, {
    headers: { "Idempotency-Key": operation.idempotencyKey },
  });
  return data;
}

export async function deleteProduct(
  operation: ProductDeleteOperation
): Promise<boolean> {
  const { data } = await api.delete<boolean>(`/products/${operation.request.productId}`, {
    data: { version: operation.request.version },
    headers: { "Idempotency-Key": operation.idempotencyKey },
  });
  return data;
}
