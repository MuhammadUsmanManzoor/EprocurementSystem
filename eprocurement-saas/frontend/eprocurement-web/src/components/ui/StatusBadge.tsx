import { Badge } from "./Badge";

const statusTone: Record<string, "green" | "blue" | "amber" | "red" | "slate" | "teal"> = {
  Draft: "slate",
  Submitted: "blue",
  Approved: "green",
  Rejected: "red",
  Published: "teal",
  Closed: "slate",
  PendingApproval: "amber",
  Issued: "green",
  Active: "green",
  Open: "blue",
  Opened: "blue",
  Evaluated: "green",
  Revised: "blue",
  Awarded: "green",
  Cancelled: "red",
  Disqualified: "red",
  Pending: "amber"
};

export function StatusBadge({ status }: { status: string }) {
  return <Badge tone={statusTone[status] ?? "slate"}>{status}</Badge>;
}
