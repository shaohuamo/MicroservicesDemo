import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ProductFormDialog } from "@/components/products/product-form-dialog";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ProductResponse } from "@/types/product";
import type { ReactNode } from "react";

vi.mock("@/lib/api/products", () => ({
  addProduct: vi.fn().mockResolvedValue({ productId: "new", productName: "X", unitPrice: 1, quantityInStock: 1 }),
  updateProduct: vi.fn().mockResolvedValue({ productId: "1", productName: "Y", unitPrice: 2, quantityInStock: 2 }),
}));

function Wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

const existingProduct: ProductResponse = {
  productId: "1",
  productName: "Existing",
  unitPrice: 10.0,
  quantityInStock: 50,
};

describe("ProductFormDialog", () => {
  it("renders nothing when closed", () => {
    const { container } = render(
      <Wrapper>
        <ProductFormDialog open={false} onOpenChange={vi.fn()} />
      </Wrapper>
    );
    expect(container.innerHTML).toBe("");
  });

  it("shows 'Add Product' title when no product prop", () => {
    render(
      <Wrapper>
        <ProductFormDialog open={true} onOpenChange={vi.fn()} />
      </Wrapper>
    );
    expect(screen.getByText("Add Product")).toBeInTheDocument();
  });

  it("shows 'Edit Product' title and pre-fills fields", () => {
    render(
      <Wrapper>
        <ProductFormDialog open={true} onOpenChange={vi.fn()} product={existingProduct} />
      </Wrapper>
    );
    expect(screen.getByText("Edit Product")).toBeInTheDocument();
    expect(screen.getByDisplayValue("Existing")).toBeInTheDocument();
    expect(screen.getByDisplayValue("10")).toBeInTheDocument();
    expect(screen.getByDisplayValue("50")).toBeInTheDocument();
  });

  it("resets form state when reopened for Add after Edit (stale state fix)", () => {
    const { rerender } = render(
      <Wrapper>
        <ProductFormDialog
          key="edit-open"
          open={true}
          onOpenChange={vi.fn()}
          product={existingProduct}
        />
      </Wrapper>
    );
    // Close
    rerender(
      <Wrapper>
        <ProductFormDialog
          key="edit-closed"
          open={false}
          onOpenChange={vi.fn()}
          product={existingProduct}
        />
      </Wrapper>
    );
    // Reopen for Add (no product)
    rerender(
      <Wrapper>
        <ProductFormDialog key="add-open" open={true} onOpenChange={vi.fn()} />
      </Wrapper>
    );
    expect(screen.getByText("Add Product")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Enter product name")).toHaveValue("");
  });

  it("shows validation error for empty product name on submit", async () => {
    const user = userEvent.setup();
    render(
      <Wrapper>
        <ProductFormDialog open={true} onOpenChange={vi.fn()} />
      </Wrapper>
    );
    await user.click(screen.getByText("Add"));
    expect(screen.getByText("Product Name can't be blank")).toBeInTheDocument();
  });
});
