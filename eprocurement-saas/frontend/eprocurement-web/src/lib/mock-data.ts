export type Status =
  | "Draft"
  | "Submitted"
  | "Approved"
  | "Rejected"
  | "Published"
  | "Closed"
  | "PendingApproval"
  | "Issued"
  | "Active"
  | "Open"
  | "Evaluated"
  | "Pending";

export const tenantName = "AKPK Demo";

export const metrics = [
  { label: "Open PRs", value: "24", trend: "+8%", tone: "teal" },
  { label: "Active Tenders", value: "9", trend: "3 closing soon", tone: "blue" },
  { label: "Pending Approvals", value: "12", trend: "-2 today", tone: "amber" },
  { label: "Contract Value", value: "$4.8M", trend: "YTD", tone: "slate" }
];

export const workflow = [
  "Purchase Request",
  "Approval",
  "Tender/RFQ/RFP",
  "Vendor Bidding",
  "Bid Opening",
  "Evaluation",
  "Award Decision",
  "Purchase Order",
  "Contract"
];

export const purchaseRequests = [
  { id: "PR-2026-0142", title: "Laptop refresh for finance division", department: "Finance", requester: "Amina Rahman", amount: 84200, status: "Submitted" as Status, date: "2026-06-12" },
  { id: "PR-2026-0138", title: "Security services renewal", department: "Facilities", requester: "Daniel Lim", amount: 320000, status: "Approved" as Status, date: "2026-06-09" },
  { id: "PR-2026-0135", title: "Network equipment for branch rollout", department: "IT", requester: "Farah Noor", amount: 215500, status: "Draft" as Status, date: "2026-06-05" },
  { id: "PR-2026-0129", title: "Training vendor for procurement policy", department: "HR", requester: "Omar Aziz", amount: 46000, status: "Rejected" as Status, date: "2026-05-30" }
];

export const tenders = [
  { id: "TEN-2026-0081", title: "Managed endpoint protection platform", method: "RFP", visibility: "Public", closing: "2026-07-02", status: "Published" as Status, bids: 6 },
  { id: "TEN-2026-0074", title: "Office renovation package", method: "Tender", visibility: "Invited", closing: "2026-06-27", status: "Published" as Status, bids: 4 },
  { id: "TEN-2026-0068", title: "Annual stationery framework", method: "RFQ", visibility: "Public", closing: "2026-06-18", status: "Closed" as Status, bids: 11 },
  { id: "TEN-2026-0061", title: "Data center maintenance", method: "RFP", visibility: "Invited", closing: "2026-06-14", status: "Evaluated" as Status, bids: 3 }
];

export const bids = [
  { id: "BID-4318", tender: "TEN-2026-0081", vendor: "NexaTech Solutions", amount: 186000, score: 91, status: "Submitted" as Status },
  { id: "BID-4314", tender: "TEN-2026-0081", vendor: "Awan Digital", amount: 172500, score: 88, status: "Submitted" as Status },
  { id: "BID-4297", tender: "TEN-2026-0068", vendor: "SupplyPro MY", amount: 43500, score: 84, status: "Evaluated" as Status }
];

export const purchaseOrders = [
  { id: "PO-2026-0044", vendor: "SupplyPro MY", tender: "TEN-2026-0068", amount: 43500, status: "Issued" as Status, date: "2026-06-11" },
  { id: "PO-2026-0039", vendor: "Metro Facilities", tender: "TEN-2026-0051", amount: 126000, status: "Issued" as Status, date: "2026-05-28" }
];

export const contracts = [
  { id: "CON-2026-0028", title: "Stationery framework agreement", vendor: "SupplyPro MY", po: "PO-2026-0044", value: 43500, status: "Active" as Status },
  { id: "CON-2026-0021", title: "Facilities support contract", vendor: "Metro Facilities", po: "PO-2026-0039", value: 126000, status: "Active" as Status }
];

export const documents = [
  { id: "DOC-1104", name: "Tender specification.pdf", entity: "Tender", owner: "Procurement", status: "Approved" as Status },
  { id: "DOC-1097", name: "Vendor compliance.zip", entity: "Bid", owner: "Committee", status: "Pending" as Status }
];

export const notifications = [
  { id: "N-9001", subject: "Tender TEN-2026-0081 closes in 14 days", channel: "Email", status: "Pending" as Status },
  { id: "N-8994", subject: "Purchase request PR-2026-0142 awaits approval", channel: "In-app", status: "Open" as Status }
];

export const auditLogs = [
  { id: "AUD-7004", action: "TenderPublished", actor: "procurement@akpk.com", entity: "Tender", date: "2026-06-16" },
  { id: "AUD-6998", action: "BidSubmitted", actor: "vendor@demo.com", entity: "Bid", date: "2026-06-15" },
  { id: "AUD-6981", action: "PurchaseRequestApproved", actor: "approver@akpk.com", entity: "PurchaseRequest", date: "2026-06-14" }
];
