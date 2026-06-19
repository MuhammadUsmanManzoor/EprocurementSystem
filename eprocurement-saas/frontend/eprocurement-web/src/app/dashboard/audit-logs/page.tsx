 "use client";

import { RecordListPage } from "@/components/pages/RecordListPage";
import { auditLogs } from "@/lib/mock-data";
import { formatDate } from "@/lib/utils";

export default function AuditLogsPage() {
  return <RecordListPage title="Audit Logs" description="Immutable record of important procurement actions across tenant services." actionLabel="Export Logs" rows={auditLogs} columns={[
    { key: "id", label: "Log ID" },
    { key: "action", label: "Action" },
    { key: "actor", label: "Actor" },
    { key: "entity", label: "Entity" },
    { key: "date", label: "Date", render: (item) => formatDate(String(item.date)) }
  ]} />;
}
