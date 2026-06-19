export function LoadingSkeleton() {
  return (
    <div className="space-y-3">
      <div className="h-5 w-1/3 animate-pulse rounded bg-slate-200" />
      <div className="h-10 animate-pulse rounded bg-slate-200" />
      <div className="h-10 animate-pulse rounded bg-slate-200" />
      <div className="h-10 animate-pulse rounded bg-slate-200" />
    </div>
  );
}
