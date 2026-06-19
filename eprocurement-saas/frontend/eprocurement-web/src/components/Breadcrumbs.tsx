"use client";

import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { usePathname } from "next/navigation";

export function Breadcrumbs() {
  const pathname = usePathname();
  const parts = pathname.split("/").filter(Boolean);
  const crumbs = parts.map((part, index) => ({
    label: part.replace(/-/g, " "),
    href: `/${parts.slice(0, index + 1).join("/")}`
  }));

  return (
    <div className="mb-4 flex flex-wrap items-center gap-1 text-xs capitalize text-slate-500">
      <Link href="/dashboard" className="hover:text-brand">Home</Link>
      {crumbs.slice(1).map((crumb) => (
        <div key={crumb.href} className="flex items-center gap-1">
          <ChevronRight size={13} />
          <Link href={crumb.href} className="hover:text-brand">{crumb.label}</Link>
        </div>
      ))}
    </div>
  );
}
