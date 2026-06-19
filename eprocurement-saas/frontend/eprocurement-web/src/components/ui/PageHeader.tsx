import { ReactNode } from "react";
import { Button } from "./Button";

export function PageHeader({ title, description, actionLabel, onAction, action }: { title: string; description?: string; actionLabel?: string; onAction?: () => void; action?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
      <div>
        <h1 className="text-2xl font-semibold tracking-normal text-ink">{title}</h1>
        {description ? <p className="mt-2 text-sm text-slate-600">{description}</p> : null}
      </div>
      {action}
      {actionLabel ? <Button onClick={onAction}>{actionLabel}</Button> : null}
    </div>
  );
}
