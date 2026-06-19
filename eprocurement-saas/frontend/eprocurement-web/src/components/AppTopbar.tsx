"use client";

import { Bell, ChevronDown, LogOut, Menu, Search, UserRound } from "lucide-react";
import { usePathname, useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import type { AuthenticatedUser } from "@/lib/api";
import { tenantName } from "@/lib/mock-data";
import { clearSession } from "@/lib/session";
import { Button } from "./ui/Button";
import { Input } from "./ui/Input";

function labelFromPath(pathname: string) {
  const parts = pathname.split("/").filter(Boolean).slice(1);
  return parts.length ? parts.map((part) => part.replace(/-/g, " ")).join(" / ") : "Dashboard";
}

export function AppTopbar({ user, onMenu }: { user: AuthenticatedUser | null; onMenu: () => void }) {
  const router = useRouter();
  const pathname = usePathname();
  const [profileOpen, setProfileOpen] = useState(false);
  const breadcrumb = useMemo(() => labelFromPath(pathname), [pathname]);

  function signOut() {
    clearSession();
    router.replace("/login");
  }

  return (
    <header className="sticky top-0 z-30 border-b border-line bg-white">
      <div className="flex h-16 items-center gap-4 px-4 lg:px-6">
        <button className="rounded-md p-2 text-slate-600 hover:bg-slate-100 md:hidden" onClick={onMenu} type="button" title="Open menu">
          <Menu size={20} />
        </button>
        <div className="min-w-0 flex-1">
          <div className="text-xs capitalize text-slate-500">{breadcrumb}</div>
          <div className="truncate text-sm font-semibold text-ink">{tenantName}</div>
        </div>
        <div className="hidden w-80 items-center gap-2 rounded-md border border-line bg-field px-3 lg:flex">
          <Search size={16} className="text-slate-500" />
          <Input className="h-9 border-0 bg-transparent px-0 focus:ring-0" placeholder="Search tenders, PRs, vendors" />
        </div>
        <button className="relative rounded-md border border-line bg-white p-2 text-slate-600 hover:bg-slate-50" type="button" title="Notifications">
          <Bell size={19} />
          <span className="absolute right-1.5 top-1.5 h-2 w-2 rounded-full bg-red-500" />
        </button>
        <div className="relative">
          <button className="flex h-10 items-center gap-2 rounded-md border border-line bg-white px-3 text-sm hover:bg-slate-50" onClick={() => setProfileOpen((value) => !value)} type="button">
            <UserRound size={17} className="text-slate-500" />
            <span className="hidden max-w-36 truncate sm:inline">{user?.fullName ?? "User"}</span>
            <ChevronDown size={15} className="text-slate-500" />
          </button>
          {profileOpen ? (
            <div className="absolute right-0 mt-2 w-64 rounded-lg border border-line bg-white p-3 shadow-lg">
              <div className="border-b border-line pb-3">
                <p className="truncate text-sm font-semibold text-ink">{user?.fullName}</p>
                <p className="mt-1 truncate text-xs text-slate-500">{user?.email}</p>
              </div>
              <Button className="mt-3 w-full justify-start" variant="ghost" onClick={signOut}>
                <LogOut size={16} />
                Sign out
              </Button>
            </div>
          ) : null}
        </div>
      </div>
    </header>
  );
}
