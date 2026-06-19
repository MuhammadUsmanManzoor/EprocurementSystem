 "use client";

import { RecordListPage } from "@/components/pages/RecordListPage";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { notifications } from "@/lib/mock-data";

export default function NotificationsPage() {
  return <RecordListPage title="Notifications" description="Email and in-app notification queue for approvals, tender events, and vendor communication." actionLabel="Create Notification" rows={notifications} columns={[
    { key: "id", label: "ID" },
    { key: "subject", label: "Subject" },
    { key: "channel", label: "Channel" },
    { key: "status", label: "Status", render: (item) => <StatusBadge status={item.status} /> }
  ]} />;
}
