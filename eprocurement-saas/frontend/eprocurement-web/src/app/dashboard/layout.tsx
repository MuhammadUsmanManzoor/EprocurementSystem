"use client";

import { ReactNode, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import type { AuthenticatedUser } from "@/lib/api";
import { getCurrentUser } from "@/lib/session";
import { RoleSidebar } from "@/components/RoleSidebar";
import { AppTopbar } from "@/components/AppTopbar";
import { Breadcrumbs } from "@/components/Breadcrumbs";
import { ToastProvider } from "@/components/ui/ToastProvider";

export default function DashboardLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(false);

  useEffect(() => {
    const currentUser = getCurrentUser();
    if (!currentUser) {
      router.replace("/login");
      return;
    }
    setUser(currentUser);
  }, [router]);

  return (
    <ToastProvider>
      <div className="grid min-h-screen bg-[#f3f5f8] md:grid-cols-[280px_1fr]">
        <div className="hidden md:block">
          <RoleSidebar user={user} />
        </div>
        {sidebarOpen ? (
          <div className="fixed inset-0 z-40 md:hidden">
            <button className="absolute inset-0 bg-slate-950/40" onClick={() => setSidebarOpen(false)} type="button" title="Close menu" />
            <div className="relative h-full w-72">
              <RoleSidebar user={user} />
            </div>
          </div>
        ) : null}
        <div className="min-w-0">
          <AppTopbar user={user} onMenu={() => setSidebarOpen(true)} />
          <main className="px-4 py-6 lg:px-6">
            <Breadcrumbs />
            {children}
          </main>
        </div>
      </div>
    </ToastProvider>
  );
}
