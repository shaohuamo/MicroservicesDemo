import { describe, it, expect, vi } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useConsulServices, useServiceHealth } from "@/hooks/use-health";
import * as healthApi from "@/lib/api/health";
import type { ReactNode } from "react";

vi.mock("@/lib/api/health");

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  Wrapper.displayName = "UseHealthQueryClientWrapper";
  return Wrapper;
}

describe("useConsulServices", () => {
  it("fetches consul services", async () => {
    const services = {
      "svc-1": { ID: "svc-1", Service: "productservice", Tags: [], Address: "10.0.0.1", Port: 8080, Meta: {} },
    };
    vi.mocked(healthApi.getConsulServices).mockResolvedValue(services);

    const { result } = renderHook(() => useConsulServices(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(services);
  });
});

describe("useServiceHealth", () => {
  it("is disabled when serviceName is undefined", () => {
    const { result } = renderHook(() => useServiceHealth(undefined), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe("idle");
  });

  it("fetches health when serviceName is provided", async () => {
    const healthEntries = [
      {
        Node: { Node: "consul", Address: "127.0.0.1" },
        Service: { ID: "svc-1", Service: "productservice", Tags: [], Address: "", Port: 8080 },
        Checks: [{ CheckID: "serfHealth", Name: "Serf Health", Status: "passing", Output: "" }],
      },
    ];
    vi.mocked(healthApi.getConsulServiceHealth).mockResolvedValue(healthEntries);

    const { result } = renderHook(() => useServiceHealth("productservice"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(healthEntries);
  });
});
