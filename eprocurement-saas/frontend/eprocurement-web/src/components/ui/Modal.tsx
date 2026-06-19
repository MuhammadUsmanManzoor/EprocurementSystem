"use client";

import { X } from "lucide-react";
import { Button } from "./Button";

export function Modal({ open, title, children, onClose }: { open: boolean; title: string; children: React.ReactNode; onClose: () => void }) {
  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 px-4">
      <section className="w-full max-w-lg rounded-lg bg-white shadow-xl">
        <header className="flex h-14 items-center justify-between border-b border-line px-5">
          <h2 className="font-semibold text-ink">{title}</h2>
          <button className="rounded-md p-2 text-slate-500 hover:bg-slate-100" onClick={onClose} type="button" title="Close">
            <X size={18} />
          </button>
        </header>
        <div className="p-5">{children}</div>
      </section>
    </div>
  );
}

export function ModalActions({ onCancel, children }: { onCancel: () => void; children: React.ReactNode }) {
  return (
    <div className="mt-6 flex justify-end gap-3">
      <Button variant="secondary" onClick={onCancel} type="button">Cancel</Button>
      {children}
    </div>
  );
}
