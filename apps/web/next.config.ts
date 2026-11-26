import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // The root layout lives at app/[lang]/layout.tsx (html lang/dir from the route
  // segment), so the global 404 must bypass layouts via app/global-not-found.tsx.
  experimental: {
    globalNotFound: true,
  },
  cacheComponents: true,
  partialPrefetching: true,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
