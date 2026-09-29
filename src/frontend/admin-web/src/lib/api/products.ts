import type {
  ProductAddRequest,
  ProductAddOperation,
  ProductDeleteRequest,
  ProductResponse,
  ProductUpdateRequest,
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
  request: ProductUpdateRequest
): Promise<ProductResponse> {
  const { data } = await api.put<ProductResponse>("/products", request);
  return data;
}

export async function deleteProduct(
  request: ProductDeleteRequest
): Promise<boolean> {
  const { data } = await api.delete<boolean>(`/products/${request.productId}`, {
    data: { version: request.version },
  });
  return data;
}
