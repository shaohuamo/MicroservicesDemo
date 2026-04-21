export interface ProductResponse {
  productId: string;
  productName: string | null;
  unitPrice: number | null;
  quantityInStock: number | null;
}

export interface ProductAddRequest {
  productName: string;
  unitPrice: number;
  quantityInStock: number;
}

export interface ProductUpdateRequest {
  productId: string;
  productName: string;
  unitPrice: number;
  quantityInStock: number;
}
