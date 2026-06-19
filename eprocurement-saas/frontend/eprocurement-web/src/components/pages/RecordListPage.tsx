"use client";

import { useState } from "react";
import { Plus, SlidersHorizontal } from "lucide-react";
import { Column, DataTable } from "@/components/ui/DataTable";
import { PageHeader } from "@/components/ui/PageHeader";
import { Card, CardBody } from "@/components/ui/Card";
import { Select } from "@/components/ui/Select";
import { EmptyState } from "@/components/ui/EmptyState";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { useToast } from "@/components/ui/ToastProvider";
import { StatusBadge } from "@/components/ui/StatusBadge";
import type { Status } from "@/lib/mock-data";

export function RecordListPage<T extends Record<string, unknown>>({
  title,
  description,
  rows,
  columns,
  actionLabel = "New record",
  emptyTitle = "No records found",
  emptyDescription = "Records will appear here once your team starts using this workflow."
}: {
  title: string;
  description: string;
  rows: T[];
  columns: Column<T>[];
  actionLabel?: string;
  emptyTitle?: string;
  emptyDescription?: string;
}) {
  const [status, setStatus] = useState("All");
  const [confirmOpen, setConfirmOpen] = useState(false);
  const { showToast } = useToast();
  const statuses = ["All", ...Array.from(new Set(rows.map((row) => String(row.status ?? "All"))))];
  const filteredRows = status === "All" ? rows : rows.filter((row) => row.status === status);

  return (
    <>
      <PageHeader title={title} description={description} actionLabel={actionLabel} onAction={() => setConfirmOpen(true)} />
      <Card className="mb-5">
        <CardBody className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div className="flex items-center gap-2 text-sm font-medium text-slate-700">
            <SlidersHorizontal size={17} />
            Search and filters
          </div>
          <div className="grid gap-3 sm:grid-cols-2 md:w-[420px]">
            <Select value={status} onChange={(event) => setStatus(event.target.value)}>
              {statuses.map((item) => (
                <option key={item}>{item}</option>
              ))}
            </Select>
            <Select defaultValue="Newest">
              <option>Newest</option>
              <option>Oldest</option>
              <option>Highest value</option>
            </Select>
          </div>
        </CardBody>
      </Card>
      {filteredRows.length ? <DataTable data={filteredRows} columns={columns} /> : <EmptyState title={emptyTitle} description={emptyDescription} actionLabel={actionLabel} />}
      <ConfirmationDialog
        open={confirmOpen}
        title={actionLabel}
        description="This action will open the workflow form in the next implementation step. The UI pattern is ready for API-backed submission."
        confirmLabel="Continue"
        onCancel={() => setConfirmOpen(false)}
        onConfirm={() => {
          setConfirmOpen(false);
          showToast("Workflow queued", `${actionLabel} is ready to connect to the API.`);
        }}
      />
    </>
  );
}

export function statusColumn<T extends { status: Status }>(): Column<T> {
  return {
    key: "status",
    label: "Status",
    render: (item) => {
      return <StatusBadge status={item.status} />;
    }
  };
}
