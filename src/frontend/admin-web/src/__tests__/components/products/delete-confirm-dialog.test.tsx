import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { DeleteConfirmDialog } from "@/components/products/delete-confirm-dialog";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ProductResponse } from "@/types/product";
import type { ReactNode } from "react";

vi.mock("@/lib/api/products", () => ({
  deleteProduct: vi.fn().mockResolvedValue(true),
}));

function Wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

const product: ProductResponse = {
  productId: "abc-123",
  productName: "Test Widget",
  unitPrice: 9.99,
  quantityInStock: 5,
};

describe("DeleteConfirmDialog", () => {
  it("renders nothing when closed", () => {
    const { container } = render(
      <Wrapper>
        <DeleteConfirmDialog open={false} onOpenChange={vi.fn()} product={product} />
      </Wrapper>
    );
    expect(container.innerHTML).toBe("");
  });

  it("shows product name in confirmation message", () => {
    render(
      <Wrapper>
        <DeleteConfirmDialog open={true} onOpenChange={vi.fn()} product={product} />
      </Wrapper>
    );
    expect(screen.getByText("Test Widget")).toBeInTheDocument();
    expect(screen.getByText("Delete Product")).toBeInTheDocument();
  });

  it("calls onOpenChange(false) when Cancel is clicked", async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();
    render(
      <Wrapper>
        <DeleteConfirmDialog open={true} onOpenChange={onOpenChange} product={product} />
      </Wrapper>
    );
    await user.click(screen.getByText("Cancel"));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});
