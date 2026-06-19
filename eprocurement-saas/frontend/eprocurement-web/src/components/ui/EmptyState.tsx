import { Inbox } from "lucide-react";
import { Button } from "./Button";

export function EmptyState({ title, description, actionLabel }: { title: string; description: string; actionLabel?: string }) {
  return (
    <div className="flex min-h-56 flex-col items-center justify-center rounded-lg border border-dashed border-line bg-white p-8 text-center">
      <div className="flex h-11 w-11 items-center justify-center rounded-md bg-slate-100 text-slate-500">
        <Inbox size={22} />
      </div>
      <h3 className="mt-4 text-base font-semibold text-ink">{title}</h3>
      <p className="mt-2 max-w-sm text-sm text-slate-600">{description}</p>
      {actionLabel ? <Button className="mt-5">{actionLabel}</Button> : null}
    </div>
  );
}
