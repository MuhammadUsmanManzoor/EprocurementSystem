"use client";

import { CheckCircle2, X } from "lucide-react";
import { createContext, useContext, useMemo, useState } from "react";

type Toast = { id: number; title: string; message?: string };
type ToastContextValue = { showToast: (title: string, message?: string) => void };

const ToastContext = createContext<ToastContextValue | null>(null);

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const value = useMemo(
    () => ({
      showToast(title: string, message?: string) {
        const toast = { id: Date.now(), title, message };
        setToasts((current) => [...current, toast]);
        setTimeout(() => setToasts((current) => current.filter((item) => item.id !== toast.id)), 3500);
      }
    }),
    []
  );

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="fixed right-4 top-4 z-50 space-y-3">
        {toasts.map((toast) => (
          <div key={toast.id} className="w-80 rounded-lg border border-line bg-white p-4 shadow-lg">
            <div className="flex items-start gap-3">
              <CheckCircle2 className="mt-0.5 text-brand" size={18} />
              <div className="min-w-0 flex-1">
                <p className="text-sm font-semibold text-ink">{toast.title}</p>
                {toast.message ? <p className="mt-1 text-sm text-slate-600">{toast.message}</p> : null}
              </div>
              <button onClick={() => setToasts((current) => current.filter((item) => item.id !== toast.id))} title="Dismiss" type="button">
                <X size={16} className="text-slate-400" />
              </button>
            </div>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const context = useContext(ToastContext);
  if (!context) throw new Error("useToast must be used inside ToastProvider");
  return context;
}
