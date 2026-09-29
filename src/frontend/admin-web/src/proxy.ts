import { NextResponse } from "next/server";
import type { NextFetchEvent, NextRequest } from "next/server";
import { auth } from "@/auth";

const PROTECTED_PATHS = ["/products"];

export async function proxy(request: NextRequest, event: NextFetchEvent) {
  const authenticatedProxy = await auth(async (request, _event: NextFetchEvent) => {
    // The second parameter selects Auth.js's proxy overload.
    void _event;
    const isProtectedPath = PROTECTED_PATHS.some((path) =>
      request.nextUrl.pathname.startsWith(path)
    );

    if (!isProtectedPath || request.auth) {
      return NextResponse.next();
    }

    const loginUrl = new URL("/login", request.nextUrl.origin);
    loginUrl.searchParams.set("callbackUrl", `${request.nextUrl.pathname}${request.nextUrl.search}`);

    return NextResponse.redirect(loginUrl);
  });

  return authenticatedProxy(request, event);
}

export const config = {
  matcher: ["/products/:path*"],
};
