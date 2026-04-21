import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { Sidebar } from "@/components/layout/sidebar";

// Mock Next.js navigation
vi.mock("next/navigation", () => ({
  usePathname: () => "/products",
}));

describe("Sidebar", () => {
  it("renders navigation links", () => {
    render(<Sidebar />);
    expect(screen.getByText("Products")).toBeInTheDocument();
    expect(screen.getByText("Health")).toBeInTheDocument();
    expect(screen.getByText("Microservices Admin")).toBeInTheDocument();
  });

  it("highlights active Products link", () => {
    render(<Sidebar />);
    const productsLink = screen.getByText("Products").closest("a");
    expect(productsLink?.className).toContain("bg-gray-200");
  });

  it("does not highlight inactive Health link", () => {
    render(<Sidebar />);
    const healthLink = screen.getByText("Health").closest("a");
    expect(healthLink?.className).not.toContain("bg-gray-200");
  });
});
