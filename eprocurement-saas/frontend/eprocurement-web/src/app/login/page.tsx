"use client";

import { FormEvent, useState } from "react";
import { Building2, LockKeyhole, Mail, ShieldCheck } from "lucide-react";
import { login } from "@/lib/api";
import { saveSession } from "@/lib/session";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";

export default function LoginPage() {
  const [email, setEmail] = useState("tenantadmin@akpk.com");
  const [password, setPassword] = useState("Password123!");
  const [error, setError] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError("");

    if (!email.includes("@")) {
      setError("Enter a valid business email address.");
      return;
    }
    if (password.length < 8) {
      setError("Password must be at least 8 characters.");
      return;
    }

    setIsSubmitting(true);
    try {
      const result = await login(email, password);
      saveSession(result.accessToken, result.user);
      window.location.assign("/dashboard");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="grid min-h-screen bg-[#eef2f5] lg:grid-cols-[1fr_480px]">
      <section className="hidden bg-ink px-10 py-12 text-white lg:flex lg:flex-col lg:justify-between">
        <div>
          <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-brand">
            <ShieldCheck size={26} />
          </div>
          <h1 className="mt-10 max-w-xl text-4xl font-semibold tracking-normal">Enterprise procurement control for every sourcing decision.</h1>
          <p className="mt-5 max-w-xl text-sm leading-6 text-slate-300">
            Manage purchase requests, approvals, tenders, bids, awards, purchase orders, contracts, audit trails, and vendor collaboration from one secure workspace.
          </p>
        </div>
        <div className="grid max-w-2xl grid-cols-3 gap-4 text-sm text-slate-300">
          <div className="border-t border-slate-600 pt-4">Tenant-aware records</div>
          <div className="border-t border-slate-600 pt-4">Role-based access</div>
          <div className="border-t border-slate-600 pt-4">Audit-ready workflows</div>
        </div>
      </section>

      <section className="flex items-center justify-center px-4 py-10">
        <form onSubmit={onSubmit} className="w-full max-w-md rounded-lg border border-line bg-white p-8 shadow-sm">
          <div className="mb-8 flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-md bg-teal-50 text-brand">
              <Building2 size={22} />
            </div>
            <div>
              <h2 className="text-xl font-semibold text-ink">Sign in</h2>
              <p className="text-sm text-slate-500">E-Procurement SaaS</p>
            </div>
          </div>

          <label className="mb-2 block text-sm font-medium text-slate-700" htmlFor="email">Email</label>
          <div className="mb-5 flex items-center gap-2 rounded-md border border-line bg-white px-3 focus-within:border-brand focus-within:ring-2 focus-within:ring-teal-100">
            <Mail size={18} className="text-slate-500" />
            <Input id="email" className="border-0 px-0 focus:ring-0" value={email} onChange={(event) => setEmail(event.target.value)} type="email" required />
          </div>

          <label className="mb-2 block text-sm font-medium text-slate-700" htmlFor="password">Password</label>
          <div className="flex items-center gap-2 rounded-md border border-line bg-white px-3 focus-within:border-brand focus-within:ring-2 focus-within:ring-teal-100">
            <LockKeyhole size={18} className="text-slate-500" />
            <Input id="password" className="border-0 px-0 focus:ring-0" value={password} onChange={(event) => setPassword(event.target.value)} type="password" required />
          </div>

          {error ? <p className="mt-4 rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p> : null}

          <Button className="mt-6 w-full" disabled={isSubmitting} type="submit">
            {isSubmitting ? "Signing in" : "Sign in"}
          </Button>
          <p className="mt-5 text-center text-xs text-slate-500">Demo password: Password123!</p>
        </form>
      </section>
    </main>
  );
}
