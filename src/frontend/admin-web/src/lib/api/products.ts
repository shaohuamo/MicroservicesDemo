import type {
  ProductAddRequest,
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
  request: ProductAddRequest
): Promise<ProductResponse> {
  const { data } = await api.post<ProductResponse>("/products", request);
  return data;
}

export async function updateProduct(
  request: ProductUpdateRequest
): Promise<ProductResponse> {
  const { data } = await api.put<ProductResponse>("/products", request);
  return data;
}

export async function deleteProduct(productId: string): Promise<boolean> {
  const { data } = await api.delete<boolean>(`/products/${productId}`);
  return data;
}
