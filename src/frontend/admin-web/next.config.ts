import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination:
          process.env.NEXT_PUBLIC_API_GATEWAY_URL
            ? `${process.env.NEXT_PUBLIC_API_GATEWAY_URL}/gateway/:path*`
            : "http://localhost:9080/gateway/:path*",
      },
      {
        source: "/otel/:path*",
        destination: process.env.OTEL_COLLECTOR_URL
          ? `${process.env.OTEL_COLLECTOR_URL}/:path*`
          : "http://localhost:4318/:path*",
      },
    ];
  },
};

export default nextConfig;
