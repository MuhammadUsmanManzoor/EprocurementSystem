"use client";

import { Check } from "lucide-react";
import { cn } from "@/lib/utils";

export function Wizard({ steps, currentStep }: { steps: string[]; currentStep: number }) {
  return (
    <div className="grid gap-3 md:grid-cols-3">
      {steps.map((step, index) => {
        const complete = index < currentStep;
        const active = index === currentStep;
        return (
          <div key={step} className={cn("flex items-center gap-3 rounded-lg border p-3", active ? "border-brand bg-teal-50" : "border-line bg-white")}>
            <div className={cn("flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-sm font-semibold", complete ? "bg-brand text-white" : active ? "bg-white text-brand" : "bg-slate-100 text-slate-500")}>
              {complete ? <Check size={16} /> : index + 1}
            </div>
            <span className="text-sm font-medium text-slate-700">{step}</span>
          </div>
        );
      })}
    </div>
  );
}
