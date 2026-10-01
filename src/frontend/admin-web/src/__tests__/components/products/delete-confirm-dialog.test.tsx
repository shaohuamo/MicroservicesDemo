import { beforeEach, describe, it, expect, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { DeleteConfirmDialog } from "@/components/products/delete-confirm-dialog";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ProductResponse } from "@/types/product";
import type { ReactNode } from "react";
import * as api from "@/lib/api/products";

vi.mock("@/lib/api/products", () => ({
  deleteProduct: vi.fn().mockResolvedValue(true),
}));

function Wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

const product: ProductResponse = {
  productId: "abc-123",
  displayName: "Test Widget",
  unitPrice: 9.99,
  quantityInStock: 5,
  version: 2,
};

describe("DeleteConfirmDialog", () => {
  beforeEach(() => {
    vi.mocked(api.deleteProduct).mockReset().mockResolvedValue(true);
  });
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
  it("preserves the key after network failure and changes it for a different target", async () => {
    vi.mocked(api.deleteProduct).mockRejectedValue({ isAxiosError: true });
    const onOpenChange = vi.fn();
    const { rerender } = render(
      <Wrapper><DeleteConfirmDialog open onOpenChange={onOpenChange} product={product} /></Wrapper>,
    );
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(api.deleteProduct).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByRole("button", { name: "Delete" })).toBeEnabled());
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(api.deleteProduct).toHaveBeenCalledTimes(2));
    const calls = vi.mocked(api.deleteProduct).mock.calls;
    expect(calls[1][0].idempotencyKey).toBe(calls[0][0].idempotencyKey);
    rerender(<Wrapper><DeleteConfirmDialog open onOpenChange={onOpenChange} product={{ ...product, productId: "other" }} /></Wrapper>);
    await waitFor(() => expect(screen.getByRole("button", { name: "Delete" })).toBeEnabled());
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(api.deleteProduct).toHaveBeenCalledTimes(3));
    expect(calls[2][0].idempotencyKey).not.toBe(calls[0][0].idempotencyKey);
    expect(calls[2][0].request.productId).toBe("other");
  });

  it("guards rapid delete submissions", async () => {
    let resolve!: (value: boolean) => void;
    vi.mocked(api.deleteProduct).mockImplementationOnce(() => new Promise((done) => { resolve = done; }));
    const onOpenChange = vi.fn();
    render(<Wrapper><DeleteConfirmDialog open onOpenChange={onOpenChange} product={product} /></Wrapper>);
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(api.deleteProduct).toHaveBeenCalledTimes(1));
    resolve(true);
    await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
  });
});
