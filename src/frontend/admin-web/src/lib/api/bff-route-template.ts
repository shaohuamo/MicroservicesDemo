const FALLBACK_ROUTE = "/api/[...path]";

export function resolveBffProxyRoute(method: string, target: string): string {
  let pathname: string;
  try {
    pathname = new URL(target, "http://localhost").pathname.replace(/\/$/, "") || "/";
  } catch {
    return FALLBACK_ROUTE;
  }

  const segments = pathname.split("/").filter(Boolean);
  const verb = method.toUpperCase();
  if (segments[0] !== "api") return FALLBACK_ROUTE;

  if (segments[1] === "products") {
    if (segments.length === 2 && ["GET", "POST", "PUT"].includes(verb)) {
      return "/api/products";
    }
    if (segments.length === 5 && verb === "GET"
      && segments[2] === "search" && segments[3] === "product-id") {
      return "/api/products/search/product-id/{productId}";
    }
    if (segments.length === 3 && verb === "DELETE") {
      return "/api/products/{productId}";
    }
  }

  if (segments[1] === "notifications") {
    if (segments.length === 2 && verb === "GET") {
      return "/api/notifications";
    }
    if (segments.length === 3 && verb === "POST" && segments[2] === "read-all") {
      return "/api/notifications/read-all";
    }
    if (segments.length === 4 && verb === "POST" && segments[3] === "ack") {
      return "/api/notifications/{notificationId}/ack";
    }
    if (segments.length === 4 && verb === "PUT" && segments[3] === "read") {
      return "/api/notifications/{notificationId}/read";
    }
  }

  return FALLBACK_ROUTE;
}
