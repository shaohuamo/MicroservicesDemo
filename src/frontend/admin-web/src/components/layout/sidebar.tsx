"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";

const navItems = [
  {
    href: "/products",
    label: "Products",
    icon: (
      <svg viewBox="0 0 24 24" fill="none" className="h-4 w-4" aria-hidden="true">
        <path d="M4 7.5 12 3l8 4.5M4 7.5v9L12 21m-8-13.5L12 12m8-4.5v9L12 21m8-13.5L12 12m0 9v-9" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    ),
  },
  {
    href: "/health",
    label: "Health",
    icon: (
      <svg viewBox="0 0 24 24" fill="none" className="h-4 w-4" aria-hidden="true">
        <path d="M12 4v16M4 12h16" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
        <rect x="5" y="5" width="14" height="14" rx="3" stroke="currentColor" strokeWidth="1.6" />
      </svg>
    ),
  },
];

export function Sidebar() {
  const pathname = usePathname();

  return (
    <aside className="relative z-10 border-b border-[var(--border)] bg-[color:var(--bg-panel)]/95 backdrop-blur-xl lg:min-h-screen lg:w-80 lg:border-b-0 lg:border-r">
      <div className="flex h-full flex-col px-4 py-4 sm:px-6 lg:px-6 lg:py-8">
        <div className="surface-panel-soft rounded-[1.75rem] p-4 sm:p-5">
          <div className="kicker mb-4">Control Surface</div>
          <Link href="/" className="group block">
            <div className="flex items-center gap-4">
              <div className="flex h-12 w-12 items-center justify-center rounded-2xl border border-[var(--border-strong)] bg-[var(--accent-soft)] text-[var(--accent-strong)] shadow-[0_12px_24px_rgba(0,0,0,0.18)]">
                <svg viewBox="0 0 24 24" fill="none" className="h-6 w-6" aria-hidden="true">
                  <path d="M4 12h4l2-5 4 10 2-5h4" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </div>
              <div>
                <div className="font-display text-[1.15rem] font-semibold text-[var(--text)] group-hover:text-[var(--accent-strong)]">
                  Microservices Admin
                </div>
                <p className="mt-1 text-sm text-[var(--muted)]">
                  Gateway-ready product operations
                </p>
              </div>
            </div>
          </Link>
        </div>

        <div className="mt-6 px-1">
          <div className="kicker mb-3">Navigation</div>
          <nav className="flex flex-col gap-2">
        {navItems.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className={cn(
              "group flex items-center gap-3 rounded-[1.2rem] border px-4 py-3 text-sm font-medium transition-transform hover:-translate-y-0.5",
              pathname.startsWith(item.href)
                ? "border-[var(--border-strong)] bg-[var(--accent-soft)] text-[var(--text)] shadow-[0_16px_28px_rgba(0,0,0,0.16)]"
                : "border-transparent bg-transparent text-[var(--muted)] hover:border-[var(--border)] hover:bg-[var(--surface)] hover:text-[var(--text)]"
            )}
          >
            <span className={cn(
              "flex h-9 w-9 items-center justify-center rounded-xl border transition-colors",
              pathname.startsWith(item.href)
                ? "border-[var(--border-strong)] bg-[rgba(255,255,255,0.05)] text-[var(--accent-strong)]"
                : "border-[var(--border)] bg-[rgba(255,255,255,0.02)] text-[var(--muted)] group-hover:text-[var(--accent-strong)]"
            )}>
              {item.icon}
            </span>
            <span className="flex-1">{item.label}</span>
            <span className="text-xs text-[var(--muted)]">0{item.href === "/products" ? "1" : "2"}</span>
          </Link>
        ))}
          </nav>
        </div>

        <div className="mt-6 hidden rounded-[1.5rem] border border-[var(--border)] bg-[rgba(255,255,255,0.025)] p-5 text-sm text-[var(--muted)] lg:block">
          <div className="kicker mb-3">Signal</div>
          <p className="leading-6">
            Product inventory, availability, and service health are surfaced from the same operational mesh.
          </p>
        </div>
      </div>
    </aside>
  );
}
