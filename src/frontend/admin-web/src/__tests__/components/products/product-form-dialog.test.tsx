import { beforeEach, describe, it, expect, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ProductFormDialog } from "@/components/products/product-form-dialog";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ProductResponse } from "@/types/product";
import type { ReactNode } from "react";

const mutationMocks = vi.hoisted(() => ({
  add: vi.fn(),
  update: vi.fn(),
}));

vi.mock("@/hooks/use-products", () => ({
  useAddProduct: () => ({
    mutateAsync: mutationMocks.add,
    isPending: false,
    error: null,
    isError: false,
  }),
  useUpdateProduct: () => ({
    mutateAsync: mutationMocks.update,
    isPending: false,
    error: null,
    isError: false,
  }),
}));

function Wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

const existingProduct: ProductResponse = {
  productId: "1",
  displayName: "Existing",
  unitPrice: 10.0,
  quantityInStock: 50,
  version: 1,
};

describe("ProductFormDialog", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mutationMocks.add.mockResolvedValue({
      productId: "new",
      displayName: "X",
      unitPrice: 1,
      quantityInStock: 1,
      version: 1,
    });
  });

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

  it("guards rapid double submit and generates one idempotency key", async () => {
    const user = userEvent.setup();
    let resolveRequest!: (value: ProductResponse) => void;
    mutationMocks.add.mockImplementationOnce(
      () => new Promise((resolve) => { resolveRequest = resolve; }),
    );
    const randomUuid = vi.spyOn(globalThis.crypto, "randomUUID")
      .mockReturnValue("7f277273-b334-47f9-8b59-d37aa3473665");
    render(
      <Wrapper>
        <ProductFormDialog open={true} onOpenChange={vi.fn()} />
      </Wrapper>,
    );
    await fillValidForm(user);

    const form = screen.getByRole("button", { name: "Add" }).closest("form")!;
    fireEvent.submit(form);
    fireEvent.submit(form);

    await waitFor(() => expect(mutationMocks.add).toHaveBeenCalledTimes(1));
    expect(randomUuid).toHaveBeenCalledTimes(1);
    resolveRequest({ productId: "new", displayName: "X", unitPrice: 1, quantityInStock: 1, version: 1 });
  });

  it("reuses the key after a network failure and changes it when payload changes", async () => {
    const user = userEvent.setup();
    const keys = [
      "7f277273-b334-47f9-8b59-d37aa3473665",
      "2ee9a1ec-3f2b-47ea-815e-d3c26d153429",
    ] as const;
    vi.spyOn(globalThis.crypto, "randomUUID")
      .mockReturnValueOnce(keys[0])
      .mockReturnValueOnce(keys[1]);
    mutationMocks.add
      .mockRejectedValueOnce(Object.assign(new Error("network"), { isAxiosError: true }))
      .mockRejectedValueOnce(Object.assign(new Error("network"), { isAxiosError: true }))
      .mockResolvedValueOnce({ productId: "new", displayName: "Changed", unitPrice: 1, quantityInStock: 1, version: 1 });
    render(
      <Wrapper>
        <ProductFormDialog open={true} onOpenChange={vi.fn()} />
      </Wrapper>,
    );
    await fillValidForm(user);

    const form = screen.getByRole("button", { name: "Add" }).closest("form")!;
    fireEvent.submit(form);
    await waitFor(() => expect(mutationMocks.add).toHaveBeenCalledTimes(1));
    fireEvent.submit(form);
    await waitFor(() => expect(mutationMocks.add).toHaveBeenCalledTimes(2));

    expect(mutationMocks.add.mock.calls[0][0].idempotencyKey).toBe(keys[0]);
    expect(mutationMocks.add.mock.calls[1][0].idempotencyKey).toBe(keys[0]);

    const nameInput = screen.getByPlaceholderText("Enter product name");
    await user.clear(nameInput);
    await user.type(nameInput, "Changed");
    fireEvent.submit(form);
    await waitFor(() => expect(mutationMocks.add).toHaveBeenCalledTimes(3));
    expect(mutationMocks.add.mock.calls[2][0].idempotencyKey).toBe(keys[1]);
  });
});

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByPlaceholderText("Enter product name"), "Product");
  fireEvent.change(screen.getByPlaceholderText("0.00"), { target: { value: "1" } });
  fireEvent.change(screen.getByPlaceholderText("0"), { target: { value: "1" } });
}
