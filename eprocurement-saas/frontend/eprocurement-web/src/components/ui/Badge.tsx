import { cn } from "@/lib/utils";

const tones: Record<string, string> = {
  green: "bg-emerald-50 text-emerald-700 ring-emerald-200",
  blue: "bg-blue-50 text-blue-700 ring-blue-200",
  amber: "bg-amber-50 text-amber-700 ring-amber-200",
  red: "bg-red-50 text-red-700 ring-red-200",
  slate: "bg-slate-100 text-slate-700 ring-slate-200",
  teal: "bg-teal-50 text-teal-700 ring-teal-200"
};

export function Badge({ children, tone = "slate", className }: { children: React.ReactNode; tone?: keyof typeof tones; className?: string }) {
  return <span className={cn("inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ring-1", tones[tone], className)}>{children}</span>;
}
