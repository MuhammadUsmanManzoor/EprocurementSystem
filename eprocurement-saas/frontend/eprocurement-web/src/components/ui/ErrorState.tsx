import { AlertTriangle } from "lucide-react";

export function ErrorState({ message }: { message: string }) {
  return (
    <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
      <div className="flex items-center gap-2 font-medium">
        <AlertTriangle size={18} />
        <span>Unable to load data</span>
      </div>
      <p className="mt-2">{message}</p>
    </div>
  );
}
