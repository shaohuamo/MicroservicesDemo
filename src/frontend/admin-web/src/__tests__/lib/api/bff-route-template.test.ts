import { describe, expect, it } from "vitest";
import { resolveBffProxyRoute } from "@/lib/api/bff-route-template";

describe("BFF proxy route templates", () => {
  it.each([
    ["GET", "/api/products", "/api/products"],
    ["POST", "/api/products", "/api/products"],
    ["PUT", "/api/products", "/api/products"],
    ["GET", "/api/products/search/product-id/42", "/api/products/search/product-id/{productId}"],
    ["DELETE", "/api/products/42", "/api/products/{productId}"],
    ["GET", "/api/notifications?limit=20", "/api/notifications"],
    ["POST", "/api/notifications/01a0e7e4-1ee1-7847-98d1-7537cfa68852/ack", "/api/notifications/{notificationId}/ack"],
    ["PUT", "/api/notifications/01a0e7e4-1ee1-7847-98d1-7537cfa68852/read", "/api/notifications/{notificationId}/read"],
    ["POST", "/api/notifications/read-all", "/api/notifications/read-all"],
  ])("maps %s %s to %s", (method, target, route) => {
    expect(resolveBffProxyRoute(method, target)).toBe(route);
  });

  it.each([
    ["GET", "/api/notifications/unknown-id/action?token=secret"],
    ["PATCH", "/api/products/42"],
    ["GET", "/products"],
  ])("uses a stable fallback for %s %s", (method, target) => {
    expect(resolveBffProxyRoute(method, target)).toBe("/api/[...path]");
  });
});
