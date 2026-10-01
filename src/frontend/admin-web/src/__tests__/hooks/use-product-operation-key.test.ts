import { describe, expect, it } from "vitest";
import { renderHook } from "@testing-library/react";
import { useProductOperationKey } from "@/hooks/use-product-operation-key";

describe("useProductOperationKey", () => {
  it.each([undefined, 500, 503])("keeps the same key when the result is uncertain (%s)", (status) => {
    const { result } = renderHook(useProductOperationKey);
    const request = { productId: "one", version: 1 };
    const key = result.current.getKey(request);
    result.current.handleError({ isAxiosError: true, response: status ? { status } : undefined });
    expect(result.current.getKey({ ...request })).toBe(key);
  });

  it.each([400, 401, 404, 409])("clears the key after a definite %s response", (status) => {
    const { result } = renderHook(useProductOperationKey);
    const request = { productId: "one", version: 1 };
    const key = result.current.getKey(request);
    result.current.handleError({ isAxiosError: true, response: { status } });
    expect(result.current.getKey(request)).not.toBe(key);
  });

  it("creates new keys for a changed payload, target, version, and a cleared operation", () => {
    const { result } = renderHook(useProductOperationKey);
    const request = { productId: "one", version: 1, displayName: "Original" };
    const keys = [result.current.getKey(request)];
    keys.push(result.current.getKey({ ...request, displayName: "Changed" }));
    keys.push(result.current.getKey({ ...request, productId: "two" }));
    keys.push(result.current.getKey({ ...request, version: 2 }));
    result.current.reset();
    keys.push(result.current.getKey(request));
    expect(new Set(keys).size).toBe(5);
  });
});
