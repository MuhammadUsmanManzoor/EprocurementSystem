"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  BadgeCheck,
  Bell,
  ClipboardList,
  FileCheck2,
  FileText,
  FolderOpen,
  Gavel,
  LayoutDashboard,
  PackageCheck,
  ReceiptText,
  Scale,
  ScrollText,
  Settings,
  Store,
  Users,
  type LucideIcon
} from "lucide-react";
import type { AuthenticatedUser } from "@/lib/api";
import { tenantName } from "@/lib/mock-data";
import { cn } from "@/lib/utils";

type NavItem = {
  label: string;
  href: string;
  icon: LucideIcon;
  roles: string[];
};

const items: NavItem[] = [
  { label: "Dashboard", href: "/dashboard", icon: LayoutDashboard, roles: ["*"] },
  { label: "Vendor Portal", href: "/dashboard/vendor-portal", icon: Store, roles: ["Vendor", "VendorUser"] },
  { label: "Purchase Requests", href: "/dashboard/purchase-requests", icon: ClipboardList, roles: ["TenantAdmin", "Procurement", "Approver", "Auditor"] },
  { label: "PR Approval", href: "/dashboard/pr-approval", icon: BadgeCheck, roles: ["TenantAdmin", "Approver", "Procurement", "Finance", "Auditor"] },
  { label: "Tenders", href: "/dashboard/tenders", icon: FileText, roles: ["TenantAdmin", "Procurement", "Committee", "EvaluationCommittee", "Vendor", "Auditor"] },
  { label: "My Bids", href: "/dashboard/my-bids", icon: Gavel, roles: ["Vendor", "VendorUser"] },
  { label: "Bid Opening", href: "/dashboard/bid-opening", icon: FileCheck2, roles: ["SuperAdmin", "Committee", "EvaluationCommittee"] },
  { label: "Bid Comparison", href: "/dashboard/bid-comparison", icon: Scale, roles: ["Committee", "EvaluationCommittee", "Procurement", "Finance", "Auditor"] },
  { label: "Evaluation", href: "/dashboard/evaluation", icon: Users, roles: ["Committee", "EvaluationCommittee", "Procurement", "Auditor"] },
  { label: "Award Decision", href: "/dashboard/award-decision", icon: FileCheck2, roles: ["TenantAdmin", "Procurement", "Committee", "EvaluationCommittee", "Finance", "Auditor"] },
  { label: "Purchase Orders", href: "/dashboard/purchase-orders", icon: PackageCheck, roles: ["SuperAdmin", "TenantAdmin", "Procurement", "Finance", "Vendor", "Auditor"] },
  { label: "Contracts", href: "/dashboard/contracts", icon: ScrollText, roles: ["TenantAdmin", "Procurement", "Vendor", "Auditor"] },
  { label: "Documents", href: "/dashboard/documents", icon: FolderOpen, roles: ["*"] },
  { label: "Notifications", href: "/dashboard/notifications", icon: Bell, roles: ["*"] },
  { label: "Audit Logs", href: "/dashboard/audit-logs", icon: ReceiptText, roles: ["SuperAdmin", "TenantAdmin", "Auditor"] },
  { label: "Master Data", href: "/dashboard/settings", icon: Settings, roles: ["SuperAdmin", "TenantAdmin"] }
];

function canSee(item: NavItem, role: string) {
  return item.roles.includes("*") || item.roles.includes(role);
}

export function RoleSidebar({ user, isOpen = true }: { user: AuthenticatedUser | null; isOpen?: boolean }) {
  const pathname = usePathname();
  const role = user?.role ?? "";
  const visibleItems = items.filter((item) => canSee(item, role));

  return (
    <aside className={cn("flex h-full w-full flex-col border-r border-line bg-white", !isOpen && "hidden md:flex")}>
      <div className="border-b border-line px-5 py-5">
        <div className="text-base font-semibold text-ink">E-Procurement</div>
        <div className="mt-1 text-xs text-slate-500">{tenantName}</div>
      </div>
      <nav className="flex-1 space-y-1 overflow-y-auto px-3 py-4">
        {visibleItems.map((item) => {
          const Icon = item.icon;
          const active = pathname === item.href || (item.href !== "/dashboard" && pathname.startsWith(item.href));
          return (
            <Link
              key={item.href}
              className={cn(
                "flex h-10 w-full items-center gap-3 rounded-md px-3 text-sm font-medium transition",
                active ? "bg-teal-50 text-brand" : "text-slate-700 hover:bg-slate-100 hover:text-ink"
              )}
              href={item.href}
              title={item.label}
            >
              <Icon size={18} className="shrink-0" />
              <span className="truncate">{item.label}</span>
            </Link>
          );
        })}
      </nav>
      <div className="border-t border-line px-5 py-4 text-xs text-slate-500">
        <div className="truncate font-medium text-slate-700">{user?.fullName ?? "Loading user"}</div>
        <div className="mt-1">{user?.role ?? "Role"}</div>
      </div>
    </aside>
  );
}
