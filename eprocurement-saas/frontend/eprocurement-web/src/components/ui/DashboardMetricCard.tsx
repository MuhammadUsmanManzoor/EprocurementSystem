import { ArrowUpRight } from "lucide-react";
import { Card, CardBody } from "./Card";

export function DashboardMetricCard({ label, value, trend }: { label: string; value: string; trend: string }) {
  return (
    <Card>
      <CardBody className="flex items-start justify-between">
        <div>
          <p className="text-sm text-slate-500">{label}</p>
          <p className="mt-3 text-3xl font-semibold tracking-normal text-ink">{value}</p>
          <p className="mt-2 text-sm text-slate-600">{trend}</p>
        </div>
        <div className="flex h-9 w-9 items-center justify-center rounded-md bg-teal-50 text-brand">
          <ArrowUpRight size={18} />
        </div>
      </CardBody>
    </Card>
  );
}
