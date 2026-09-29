import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  addProduct,
  deleteProduct,
  getProductById,
  getProducts,
  updateProduct,
} from "@/lib/api/products";
import type {
  ProductAddOperation,
  ProductDeleteRequest,
  ProductUpdateRequest,
} from "@/types/product";

const PRODUCTS_KEY = ["products"] as const;

export function useProducts() {
  return useQuery({
    queryKey: PRODUCTS_KEY,
    queryFn: getProducts,
  });
}

export function useProduct(productId: string | undefined) {
  return useQuery({
    queryKey: [...PRODUCTS_KEY, productId],
    queryFn: () => getProductById(productId!),
    enabled: !!productId,
  });
}

export function useAddProduct() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (operation: ProductAddOperation) => addProduct(operation),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: PRODUCTS_KEY });
    },
  });
}

export function useUpdateProduct() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ProductUpdateRequest) => updateProduct(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: PRODUCTS_KEY });
    },
  });
}

export function useDeleteProduct() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ProductDeleteRequest) => deleteProduct(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: PRODUCTS_KEY });
    },
  });
}
