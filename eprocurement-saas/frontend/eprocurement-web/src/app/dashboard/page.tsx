 "use client";

import { AlertCircle, CheckCircle2, Clock, FileText } from "lucide-react";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { DashboardMetricCard } from "@/components/ui/DashboardMetricCard";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { DataTable } from "@/components/ui/DataTable";
import { metrics, purchaseRequests, tenders, workflow } from "@/lib/mock-data";
import { formatCurrency, formatDate } from "@/lib/utils";

export default function DashboardPage() {
  return (
    <>
      <PageHeader title="Executive Dashboard" description="Tenant-wide procurement performance, risk, and workload for the current operating cycle." actionLabel="Create PR" />
      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {metrics.map((metric) => (
          <DashboardMetricCard key={metric.label} label={metric.label} value={metric.value} trend={metric.trend} />
        ))}
      </section>

      <section className="mt-6 grid gap-5 xl:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <h2 className="text-base font-semibold text-ink">Procurement Pipeline</h2>
            <p className="mt-1 text-sm text-slate-600">Live process map from request to contract.</p>
          </CardHeader>
          <CardBody>
            <div className="grid gap-3 md:grid-cols-3">
              {workflow.map((step, index) => (
                <div key={step} className="flex min-h-16 items-center gap-3 rounded-md border border-line bg-field px-4">
                  <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md bg-ink text-sm font-semibold text-white">{index + 1}</div>
                  <div className="text-sm font-medium text-slate-700">{step}</div>
                </div>
              ))}
            </div>
          </CardBody>
        </Card>

        <Card>
          <CardHeader>
            <h2 className="text-base font-semibold text-ink">Attention Required</h2>
          </CardHeader>
          <CardBody className="space-y-4">
            {[
              { icon: Clock, title: "3 tenders close this week", tone: "text-amber-700 bg-amber-50" },
              { icon: AlertCircle, title: "2 awards need justification review", tone: "text-red-700 bg-red-50" },
              { icon: CheckCircle2, title: "8 purchase requests approved", tone: "text-emerald-700 bg-emerald-50" }
            ].map((item) => {
              const Icon = item.icon;
              return (
                <div key={item.title} className="flex items-center gap-3 rounded-md border border-line p-3">
                  <div className={`flex h-9 w-9 items-center justify-center rounded-md ${item.tone}`}>
                    <Icon size={18} />
                  </div>
                  <span className="text-sm font-medium text-slate-700">{item.title}</span>
                </div>
              );
            })}
          </CardBody>
        </Card>
      </section>

      <section className="mt-6 grid gap-5 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <h2 className="text-base font-semibold text-ink">Recent Purchase Requests</h2>
          </CardHeader>
          <CardBody>
            <DataTable
              data={purchaseRequests}
              columns={[
                { key: "id", label: "PR No." },
                { key: "title", label: "Title" },
                { key: "amount", label: "Amount", render: (item) => formatCurrency(Number(item.amount)) },
                { key: "status", label: "Status", render: (item) => <StatusBadge status={item.status} /> }
              ]}
            />
          </CardBody>
        </Card>
        <Card>
          <CardHeader>
            <h2 className="text-base font-semibold text-ink">Active Tenders</h2>
          </CardHeader>
          <CardBody>
            <DataTable
              data={tenders}
              columns={[
                { key: "id", label: "Tender" },
                { key: "title", label: "Title" },
                { key: "closing", label: "Closing", render: (item) => formatDate(String(item.closing)) },
                { key: "status", label: "Status", render: (item) => <StatusBadge status={item.status} /> }
              ]}
            />
          </CardBody>
        </Card>
      </section>
    </>
  );
}
