export interface ProductResponse {
  productId: string;
  displayName: string | null;
  unitPrice: number;
  quantityInStock: number;
  version: number;
}

export interface ProductAddRequest {
  displayName: string;
  unitPrice: number;
  quantityInStock: number;
}

export interface ProductWriteOperation<TRequest> {
  request: TRequest;
  idempotencyKey: string;
}

export interface ProductDeleteRequest {
  productId: string;
  version: number;
}

export interface ProductUpdateRequest {
  productId: string;
  displayName: string;
  unitPrice: number;
  quantityInStock: number;
  version: number;
}

export type ProductAddOperation = ProductWriteOperation<ProductAddRequest>;
export type ProductUpdateOperation = ProductWriteOperation<ProductUpdateRequest>;
export type ProductDeleteOperation = ProductWriteOperation<ProductDeleteRequest>;
