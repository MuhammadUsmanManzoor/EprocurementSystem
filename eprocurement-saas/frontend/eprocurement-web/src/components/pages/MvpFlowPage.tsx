"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { ArrowLeft, CheckCircle2, ExternalLink, FilePlus2, Plus, RefreshCw } from "lucide-react";
import { api, AwardDecision, Bid, BidComparison, Contract, demoTenantId, MasterDataItem, PurchaseOrder, PurchaseRequest, Tender, Vendor } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/utils";
import { Button } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { Input } from "@/components/ui/Input";
import { PageHeader } from "@/components/ui/PageHeader";
import { Select } from "@/components/ui/Select";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Textarea } from "@/components/ui/Textarea";
import { useToast } from "@/components/ui/ToastProvider";
import { DocumentPanel } from "@/components/documents/DocumentPanel";

type Mode =
  | "purchase-requests"
  | "create-pr"
  | "pr-detail"
  | "approval"
  | "tenders"
  | "create-tender"
  | "tender-detail"
  | "vendor-tenders"
  | "submit-bid"
  | "my-bids"
  | "bid-opening"
  | "bid-comparison"
  | "award-decision"
  | "purchase-orders"
  | "contracts";

type PageState = {
  prs: PurchaseRequest[];
  tenders: Tender[];
  vendors: Vendor[];
  bids: Bid[];
  comparisons: BidComparison[];
  awards: AwardDecision[];
  purchaseOrders: PurchaseOrder[];
  contracts: Contract[];
  masterData: MasterDataItem[];
};

const emptyState: PageState = {
  prs: [],
  tenders: [],
  vendors: [],
  bids: [],
  comparisons: [],
  awards: [],
  purchaseOrders: [],
  contracts: [],
  masterData: []
};

const pageTitles: Record<Mode, { title: string; description: string }> = {
  "purchase-requests": { title: "Purchase Requests", description: "Create, submit, approve, and convert requests into tender events." },
  "create-pr": { title: "Create Purchase Request", description: "Start the procurement flow with a simple request and line item." },
  "pr-detail": { title: "Purchase Request Detail", description: "Review status, items, approval remarks, and next actions." },
  approval: { title: "PR Approval", description: "Approve submitted purchase requests before tender creation." },
  tenders: { title: "Tenders", description: "Create, publish, and monitor RFQ/RFP/tender events." },
  "create-tender": { title: "Create Tender from PR", description: "Convert an approved purchase request into a sourcing event." },
  "tender-detail": { title: "Tender Detail", description: "Review tender status, closing date, publication, and bids." },
  "vendor-tenders": { title: "Available Tenders", description: "Vendor view of published public or invited tenders." },
  "submit-bid": { title: "Submit Bid", description: "Submit or revise a vendor commercial offer before tender closing." },
  "my-bids": { title: "My Bids", description: "View submitted vendor bids and status." },
  "bid-opening": { title: "Bid Opening", description: "Open bids after tender closing as an evaluation committee user." },
  "bid-comparison": { title: "Bid Comparison", description: "Compare opened bid prices and highlight the lowest offer." },
  "award-decision": { title: "Award Decision", description: "Select the preferred bid and record the mandatory justification." },
  "purchase-orders": { title: "Purchase Orders", description: "Generate purchase orders from approved award decisions." },
  contracts: { title: "Contracts", description: "Create contracts linked to purchase orders, tenders, vendors, and tenants." }
};

export function MvpFlowPage({ mode, id }: { mode: Mode; id?: string }) {
  const router = useRouter();
  const [state, setState] = useState<PageState>(emptyState);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [selectedTenderId, setSelectedTenderId] = useState("");
  const [selectedVendorId, setSelectedVendorId] = useState("");
  const [selectedAwardId, setSelectedAwardId] = useState("");
  const [selectedPoId, setSelectedPoId] = useState("");
  const [justification, setJustification] = useState("Lowest compliant bid with acceptable delivery timeline.");
  const [prForm, setPrForm] = useState({ title: "Office Network Upgrade", department: "", costCenter: "", category: "", currency: "", justification: "Improve reliability for procurement operations.", itemCode: "", description: "", uom: "", quantity: 5, price: 1200 });
  const [tenderForm, setTenderForm] = useState({ purchaseRequestId: "", title: "", description: "", method: "RFQ", visibility: "Public", evaluationCriteria: "", committeeMember: "", documentType: "", closingDateUtc: new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 16) });
  const [bidForm, setBidForm] = useState({ amount: 48000, description: "", currency: "" });
  const [poForm, setPoForm] = useState({ paymentTerm: "", taxCode: "", deliveryLocation: "" });
  const [contractForm, setContractForm] = useState({ documentType: "" });
  const { showToast } = useToast();

  const tender = useMemo(() => state.tenders.find((item) => item.id === (id ?? selectedTenderId)) ?? state.tenders[0], [id, selectedTenderId, state.tenders]);
  const pr = useMemo(() => state.prs.find((item) => item.id === id) ?? state.prs[0], [id, state.prs]);
  const po = useMemo(() => state.purchaseOrders.find((item) => item.id === id) ?? state.purchaseOrders.find((item) => item.id === selectedPoId) ?? state.purchaseOrders[0], [id, selectedPoId, state.purchaseOrders]);
  const master = (type: string) => state.masterData.filter((item) => item.type === type && item.isActive);

  async function load() {
    setLoading(true);
    setError("");
    try {
      const [prs, tenders, vendors, bids, awards, purchaseOrders, contracts, masterData] = await Promise.all([
        api.purchaseRequests.list(),
        api.tenders.list(),
        api.vendors.list(),
        api.bids.list(),
        api.awards.list(),
        api.purchaseOrders.list(),
        api.contracts.list(),
        api.masterData.list()
      ]);
      setState((current) => ({ ...current, prs, tenders, vendors, bids, awards, purchaseOrders, contracts, masterData }));
      const firstTender = tenders[0];
      if (firstTender) {
        setSelectedTenderId((current) => current || firstTender.id);
        const comparisons = await api.bids.comparison(firstTender.id, firstTender.closingDateUtc);
        setState((current) => ({ ...current, comparisons }));
      }
      if (vendors[0]) setSelectedVendorId((current) => current || vendors[0].id);
      if (awards[0]) setSelectedAwardId((current) => current || awards[0].id);
      if (purchaseOrders[0]) setSelectedPoId((current) => current || purchaseOrders[0].id);
      setPrForm((current) => ({
        ...current,
        department: current.department || masterData.find((item) => item.type === "Department" && item.isActive)?.name || "",
        costCenter: current.costCenter || masterData.find((item) => item.type === "CostCenter" && item.isActive)?.code || "",
        category: current.category || masterData.find((item) => item.type === "Category" && item.isActive)?.code || "",
        currency: current.currency || masterData.find((item) => item.type === "Currency" && item.isActive)?.code || "USD",
        itemCode: current.itemCode || masterData.find((item) => item.type === "Item" && item.isActive)?.code || "",
        description: current.description || masterData.find((item) => item.type === "Item" && item.isActive)?.name || "",
        uom: current.uom || masterData.find((item) => item.type === "UnitOfMeasure" && item.isActive)?.code || ""
      }));
      setTenderForm((current) => ({
        ...current,
        method: current.method || masterData.find((item) => item.type === "TenderMethod" && item.isActive)?.code || "RFQ",
        evaluationCriteria: current.evaluationCriteria || masterData.find((item) => item.type === "EvaluationCriteria" && item.isActive)?.code || "",
        committeeMember: current.committeeMember || masterData.find((item) => item.type === "CommitteeMember" && item.isActive)?.code || "",
        documentType: current.documentType || masterData.find((item) => item.type === "DocumentType" && item.isActive)?.code || ""
      }));
      setBidForm((current) => ({ ...current, currency: current.currency || masterData.find((item) => item.type === "Currency" && item.isActive)?.code || "USD", description: current.description || masterData.find((item) => item.type === "Item" && item.isActive)?.name || "Compliant commercial offer" }));
      setPoForm((current) => ({
        ...current,
        paymentTerm: current.paymentTerm || masterData.find((item) => item.type === "PaymentTerm" && item.isActive)?.code || "",
        taxCode: current.taxCode || masterData.find((item) => item.type === "TaxCode" && item.isActive)?.code || "",
        deliveryLocation: current.deliveryLocation || masterData.find((item) => item.type === "DeliveryLocation" && item.isActive)?.code || ""
      }));
      setContractForm((current) => ({ ...current, documentType: current.documentType || masterData.find((item) => item.type === "DocumentType" && item.code === "CONTRACT" && item.isActive)?.code || "" }));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load procurement data.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, []);

  async function runAction(title: string, action: () => Promise<unknown>) {
    setBusy(true);
    setError("");
    try {
      await action();
      showToast(title);
      await load();
      return true;
    } catch (err) {
      setError(err instanceof Error ? err.message : "Action failed.");
      return false;
    } finally {
      setBusy(false);
    }
  }

  async function refreshComparison(tenderToLoad = tender) {
    if (!tenderToLoad) return;
    const comparisons = await api.bids.comparison(tenderToLoad.id, tenderToLoad.closingDateUtc);
    setState((current) => ({ ...current, comparisons }));
  }

  const header = pageTitles[mode];
  const headerAction = (
    <div className="flex flex-wrap items-center gap-2">
      {mode === "purchase-requests" ? (
        <Link className="inline-flex h-10 items-center justify-center gap-2 rounded-md bg-brand px-4 text-sm font-medium text-white transition hover:bg-teal-800" href="/dashboard/purchase-requests/create">
          <Plus size={16} /> Create PR
        </Link>
      ) : null}
      {mode === "create-pr" || mode === "pr-detail" ? (
        <Link className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-line bg-white px-4 text-sm font-medium text-slate-800 transition hover:bg-slate-50" href="/dashboard/purchase-requests">
          <ArrowLeft size={16} /> Back to PRs
        </Link>
      ) : null}
      {mode === "tenders" ? (
        <Link className="inline-flex h-10 items-center justify-center gap-2 rounded-md bg-brand px-4 text-sm font-medium text-white transition hover:bg-teal-800" href="/dashboard/tenders/create-from-pr">
          <Plus size={16} /> Create Tender
        </Link>
      ) : null}
      <Button variant="secondary" onClick={load} disabled={loading || busy}>
        <RefreshCw size={16} /> Refresh
      </Button>
    </div>
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title={header.title}
        description={header.description}
        action={headerAction}
      />

      {error ? <div className="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div> : null}
      {loading ? <div className="rounded-lg border border-line bg-white p-6 text-sm text-slate-500">Loading procurement workspace...</div> : null}

      {!loading && mode === "purchase-requests" ? (
        <DataTable
          data={state.prs.map((item) => ({ ...item, amount: item.items.reduce((sum, line) => sum + line.quantity * line.estimatedUnitPrice, 0) }))}
          columns={[
            { key: "docNum", label: "Doc Num", render: (item) => <Link className="font-semibold text-brand" href={`/dashboard/purchase-requests/${item.id}`}>{String(item.docNum || "-")}</Link> },
            { key: "docEntry", label: "Doc Entry", render: (item) => String(item.docEntry || "-") },
            { key: "title", label: "Title", render: (item) => <Link className="block max-w-xl truncate font-medium text-brand" title={String(item.title)} href={`/dashboard/purchase-requests/${item.id}`}>{String(item.title)}</Link> },
            { key: "department", label: "Department" },
            { key: "costCenter", label: "Cost Center", render: (item) => String(item.costCenter ?? "-") },
            { key: "category", label: "Category", render: (item) => String(item.category ?? "-") },
            { key: "amount", label: "Estimated", render: (item) => formatCurrency(Number(item.amount)) },
            { key: "status", label: "Status", render: (item) => <StatusBadge status={String(item.status)} /> },
            { key: "createdAtUtc", label: "Created", render: (item) => formatDate(String(item.createdAtUtc)) }
          ]}
        />
      ) : null}

      {!loading && mode === "create-pr" ? (
        <FormCard title="Request Information">
          <Input placeholder="Title" value={prForm.title} onChange={(event) => setPrForm({ ...prForm, title: event.target.value })} />
          <div className="grid gap-3 md:grid-cols-2">
            <MasterSelect label="Department" value={prForm.department} items={master("Department")} valueMode="name" onChange={(department) => setPrForm({ ...prForm, department })} />
            <MasterSelect label="Cost Center" value={prForm.costCenter} items={master("CostCenter")} onChange={(costCenter) => setPrForm({ ...prForm, costCenter })} />
            <MasterSelect label="Category" value={prForm.category} items={master("Category")} onChange={(category) => setPrForm({ ...prForm, category })} />
            <MasterSelect label="Currency" value={prForm.currency} items={master("Currency")} onChange={(currency) => setPrForm({ ...prForm, currency })} />
          </div>
          <Textarea placeholder="Justification" value={prForm.justification} onChange={(event) => setPrForm({ ...prForm, justification: event.target.value })} />
          <div className="grid gap-3 md:grid-cols-[1fr_160px]">
            <MasterSelect label="Item / Material" value={prForm.itemCode} items={master("Item")} onChange={(itemCode) => {
              const item = master("Item").find((record) => record.code === itemCode);
              setPrForm({ ...prForm, itemCode, description: item?.name ?? prForm.description });
            }} />
            <MasterSelect label="Unit" value={prForm.uom} items={master("UnitOfMeasure")} onChange={(uom) => setPrForm({ ...prForm, uom })} />
          </div>
          <Input placeholder="Item description" value={prForm.description} onChange={(event) => setPrForm({ ...prForm, description: event.target.value })} />
          <div className="grid gap-3 sm:grid-cols-2">
            <Input type="number" value={prForm.quantity} onChange={(event) => setPrForm({ ...prForm, quantity: Number(event.target.value) })} />
            <Input type="number" value={prForm.price} onChange={(event) => setPrForm({ ...prForm, price: Number(event.target.value) })} />
          </div>
          <Button disabled={busy || !prForm.title || !prForm.justification || !prForm.description || prForm.quantity <= 0 || prForm.price <= 0} onClick={async () => {
            const ok = await runAction("Purchase request created", () => api.purchaseRequests.create({ tenantId: demoTenantId, title: prForm.title, department: prForm.department, costCenter: prForm.costCenter, category: prForm.category, currency: prForm.currency, justification: prForm.justification, items: [{ itemCode: prForm.itemCode, description: prForm.description, unitOfMeasure: prForm.uom, quantity: prForm.quantity, estimatedUnitPrice: prForm.price }] }));
            if (ok) router.push("/dashboard/purchase-requests");
          }}>
            <FilePlus2 size={16} /> Create PR
          </Button>
          <p className="text-xs text-slate-500">The request is saved as Draft. Open it from the list to submit it for approval.</p>
        </FormCard>
      ) : null}

      {!loading && mode === "pr-detail" && pr ? (
        <PrDetail
          pr={pr}
          busy={busy}
          documentTypes={master("DocumentType")}
          onSubmit={() => runAction("Purchase request submitted", async () => {
            const linkedDocuments = await api.documents.list({ entityName: "PurchaseRequest", entityId: pr.id });
            await Promise.all(linkedDocuments.filter((document) => !document.isLocked).map((document) => api.documents.lock(document.id, "PurchaseRequestSubmitted")));
            await api.purchaseRequests.submit(pr.id);
          })}
        />
      ) : null}

      {!loading && mode === "approval" ? (
        <DataTable
          data={state.prs.filter((item) => item.status === "Submitted" || item.status === "Approved")}
          columns={[
            { key: "title", label: "Title" },
            { key: "docNum", label: "Doc Num", render: (item) => String(item.docNum || "-") },
            { key: "department", label: "Department" },
            { key: "status", label: "Status", render: (item) => <StatusBadge status={String(item.status)} /> },
            { key: "id", label: "Action", render: (item) => String(item.status) === "Submitted" ? <Button disabled={busy} onClick={() => runAction("Purchase request approved", () => api.purchaseRequests.approve(String(item.id), true, "Approved for tender creation."))}>Approve</Button> : <span className="text-slate-500">Approved</span> }
          ]}
        />
      ) : null}

      {!loading && mode === "tenders" ? (
        <DataTable
          data={state.tenders}
          columns={[
            { key: "title", label: "Title", render: (item) => <Link className="font-medium text-brand" href={`/dashboard/tenders/${item.id}`}>{String(item.title)}</Link> },
            { key: "method", label: "Method" },
            { key: "evaluationCriteria", label: "Criteria", render: (item) => String(item.evaluationCriteria ?? "-") },
            { key: "visibility", label: "Visibility" },
            { key: "closingDateUtc", label: "Closing", render: (item) => formatDate(String(item.closingDateUtc)) },
            { key: "status", label: "Status", render: (item) => <StatusBadge status={String(item.status)} /> }
          ]}
        />
      ) : null}

      {!loading && mode === "create-tender" ? (
        <FormCard title="Tender Setup">
          <Select value={tenderForm.purchaseRequestId} onChange={(event) => {
            const selectedPr = state.prs.find((item) => item.id === event.target.value);
            setTenderForm({ ...tenderForm, purchaseRequestId: event.target.value, title: selectedPr?.title ?? "", description: selectedPr?.justification ?? "" });
          }}>
            <option value="">Select approved purchase request</option>
            {state.prs.filter((item) => item.status === "Approved").map((item) => <option key={item.id} value={item.id}>{item.title}</option>)}
          </Select>
          <Input placeholder="Tender title" value={tenderForm.title} onChange={(event) => setTenderForm({ ...tenderForm, title: event.target.value })} />
          <Textarea placeholder="Tender description" value={tenderForm.description} onChange={(event) => setTenderForm({ ...tenderForm, description: event.target.value })} />
          <div className="grid gap-3 md:grid-cols-2">
            <MasterSelect label="Tender Method" value={tenderForm.method} items={master("TenderMethod")} onChange={(method) => setTenderForm({ ...tenderForm, method })} />
            <MasterSelect label="Evaluation Criteria" value={tenderForm.evaluationCriteria} items={master("EvaluationCriteria")} onChange={(evaluationCriteria) => setTenderForm({ ...tenderForm, evaluationCriteria })} />
            <MasterSelect label="Committee Member" value={tenderForm.committeeMember} items={master("CommitteeMember")} onChange={(committeeMember) => setTenderForm({ ...tenderForm, committeeMember })} />
            <MasterSelect label="Document Type" value={tenderForm.documentType} items={master("DocumentType")} onChange={(documentType) => setTenderForm({ ...tenderForm, documentType })} />
          </div>
          <div>
            <label className="mb-2 block text-sm font-medium text-slate-700">Visibility</label>
            <Select value={tenderForm.visibility} onChange={(event) => setTenderForm({ ...tenderForm, visibility: event.target.value })}>
              <option value="Public">Public</option>
              <option value="InvitedOnly">Invited only</option>
            </Select>
          </div>
          <Input type="datetime-local" value={tenderForm.closingDateUtc} onChange={(event) => setTenderForm({ ...tenderForm, closingDateUtc: event.target.value })} />
          <Button disabled={busy || !tenderForm.purchaseRequestId || !tenderForm.title} onClick={() => runAction("Tender created from approved PR", () => api.tenders.create({ tenantId: demoTenantId, purchaseRequestId: tenderForm.purchaseRequestId, title: tenderForm.title, description: tenderForm.description, method: tenderForm.method, visibility: tenderForm.visibility, evaluationCriteria: tenderForm.evaluationCriteria, committeeMember: tenderForm.committeeMember, documentType: tenderForm.documentType, closingDateUtc: new Date(tenderForm.closingDateUtc).toISOString() }))}>
            Create Tender
          </Button>
        </FormCard>
      ) : null}

      {!loading && mode === "tender-detail" && tender ? <TenderDetail tender={tender} bids={state.bids.filter((bid) => bid.tenderId === tender.id)} busy={busy} onPublish={() => runAction("Tender published", () => api.tenders.publish(tender.id))} /> : null}

      {!loading && mode === "vendor-tenders" ? (
        <DataTable data={state.tenders.filter((item) => item.status === "Published")} columns={[
          { key: "title", label: "Tender" },
          { key: "method", label: "Method" },
          { key: "closingDateUtc", label: "Closing", render: (item) => formatDate(String(item.closingDateUtc)) },
          { key: "id", label: "Action", render: (item) => <Link className="inline-flex items-center gap-2 text-sm font-medium text-brand" href="/dashboard/submit-bid">Submit Bid <ExternalLink size={14} /></Link> }
        ]} />
      ) : null}

      {!loading && mode === "submit-bid" ? (
        <FormCard title="Commercial Offer">
          <Select value={selectedTenderId} onChange={(event) => setSelectedTenderId(event.target.value)}>
            {state.tenders.filter((item) => item.status === "Published").map((item) => <option key={item.id} value={item.id}>{item.title} ({formatDate(item.closingDateUtc)})</option>)}
          </Select>
          <Select value={selectedVendorId} onChange={(event) => setSelectedVendorId(event.target.value)}>
            {state.vendors.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </Select>
          <div className="grid gap-3 md:grid-cols-[1fr_160px]">
            <MasterSelect label="Bid Item" value={bidForm.description} items={master("Item")} valueMode="name" onChange={(description) => setBidForm({ ...bidForm, description })} />
            <MasterSelect label="Currency" value={bidForm.currency} items={master("Currency")} onChange={(currency) => setBidForm({ ...bidForm, currency })} />
          </div>
          <Input type="number" value={bidForm.amount} onChange={(event) => setBidForm({ ...bidForm, amount: Number(event.target.value) })} />
          <Button disabled={busy || !tender || !selectedVendorId} onClick={() => runAction("Bid submitted", () => api.bids.submit({ tenderId: tender!.id, vendorId: selectedVendorId, tenderClosingDateUtc: tender!.closingDateUtc, currency: bidForm.currency, items: [{ description: bidForm.description, quantity: 1, unitPrice: bidForm.amount }] }))}>
            Submit Bid
          </Button>
          {tender && new Date(tender.closingDateUtc) <= new Date() ? <p className="text-sm text-amber-700">Selected tender is closed, so the API will reject new or revised bids.</p> : null}
        </FormCard>
      ) : null}

      {!loading && mode === "my-bids" ? <BidsTable bids={state.bids} /> : null}

      {!loading && mode === "bid-opening" ? (
        <FormCard title="Open Bids">
          <Select value={selectedTenderId} onChange={(event) => setSelectedTenderId(event.target.value)}>
            {state.tenders.map((item) => <option key={item.id} value={item.id}>{item.title}</option>)}
          </Select>
          {tender ? <p className="text-sm text-slate-600">Closing date: {formatDate(tender.closingDateUtc)}. Bids can open only after this date.</p> : null}
          <Button disabled={busy || !tender} onClick={() => runAction("Bids opened", () => api.bids.open(tender!.id, tender!.closingDateUtc))}>
            <CheckCircle2 size={16} /> Open Bids
          </Button>
        </FormCard>
      ) : null}

      {!loading && mode === "bid-comparison" ? (
        <div className="space-y-4">
          <TenderPicker tenders={state.tenders} value={selectedTenderId} onChange={async (value) => {
            setSelectedTenderId(value);
            const selected = state.tenders.find((item) => item.id === value);
            await refreshComparison(selected);
          }} />
          <ComparisonTable comparisons={state.comparisons} />
        </div>
      ) : null}

      {!loading && mode === "award-decision" ? (
        <FormCard title="Award Decision">
          <TenderPicker tenders={state.tenders} value={selectedTenderId} onChange={async (value) => {
            setSelectedTenderId(value);
            const selected = state.tenders.find((item) => item.id === value);
            await refreshComparison(selected);
          }} />
          <Select value={state.comparisons.find((item) => item.isLowest)?.id ?? state.comparisons[0]?.id ?? ""} onChange={() => undefined}>
            {state.comparisons.filter((item) => item.totalAmount !== null).map((item) => <option key={item.id} value={item.id}>{item.vendorEmail} - {formatCurrency(item.totalAmount ?? 0, item.currency)}</option>)}
          </Select>
          <Textarea value={justification} onChange={(event) => setJustification(event.target.value)} placeholder="Award justification" />
          <Button disabled={busy || !justification.trim() || !state.comparisons.some((item) => item.totalAmount !== null)} onClick={() => {
            const selectedBid = state.comparisons.find((item) => item.isLowest) ?? state.comparisons.find((item) => item.totalAmount !== null);
            return selectedBid ? runAction("Award decision created", () => api.awards.create({ tenantId: demoTenantId, tenderId: selectedTenderId, bidId: selectedBid.id, vendorId: selectedBid.vendorId, amount: selectedBid.totalAmount ?? 0, currency: selectedBid.currency, justification })) : undefined;
          }}>
            Create Award Decision
          </Button>
          <DataTable data={state.awards} columns={[
            { key: "vendorId", label: "Vendor" },
            { key: "amount", label: "Amount", render: (item) => formatCurrency(Number(item.amount), String(item.currency)) },
            { key: "status", label: "Status", render: (item) => <StatusBadge status={String(item.status)} /> },
            { key: "id", label: "Approve", render: (item) => String(item.status) === "PendingApproval" ? <Button disabled={busy} onClick={() => runAction("Award approved", () => api.awards.approve(String(item.id), true, "Approved for PO generation."))}>Approve</Button> : <span className="text-slate-500">Approved</span> }
          ]} />
        </FormCard>
      ) : null}

      {!loading && mode === "purchase-orders" ? (
        <FormCard title="Purchase Orders">
          <Select value={selectedAwardId} onChange={(event) => setSelectedAwardId(event.target.value)}>
            <option value="">Select approved award</option>
            {state.awards.filter((item) => item.status === "Approved").map((item) => <option key={item.id} value={item.id}>{item.vendorId} - {formatCurrency(item.amount, item.currency)}</option>)}
          </Select>
          <div className="grid gap-3 md:grid-cols-3">
            <MasterSelect label="Payment Term" value={poForm.paymentTerm} items={master("PaymentTerm")} onChange={(paymentTerm) => setPoForm({ ...poForm, paymentTerm })} />
            <MasterSelect label="Tax Code" value={poForm.taxCode} items={master("TaxCode")} onChange={(taxCode) => setPoForm({ ...poForm, taxCode })} />
            <MasterSelect label="Delivery Location" value={poForm.deliveryLocation} items={master("DeliveryLocation")} onChange={(deliveryLocation) => setPoForm({ ...poForm, deliveryLocation })} />
          </div>
          <Button disabled={busy || !selectedAwardId} onClick={() => runAction("Purchase order generated", () => api.purchaseOrders.generate(selectedAwardId, poForm))}>Generate PO</Button>
          <DataTable data={state.purchaseOrders} columns={[
            { key: "poNumber", label: "PO Number" },
            { key: "amount", label: "Amount", render: (item) => formatCurrency(Number(item.amount), String(item.currency)) },
            { key: "paymentTerm", label: "Payment", render: (item) => String(item.paymentTerm ?? "-") },
            { key: "deliveryLocation", label: "Delivery", render: (item) => String(item.deliveryLocation ?? "-") },
            { key: "status", label: "Status", render: (item) => <StatusBadge status={String(item.status)} /> },
            { key: "id", label: "Open", render: (item) => <Link className="text-brand" href={`/dashboard/purchase-orders/${item.id}`}>Detail</Link> }
          ]} />
        </FormCard>
      ) : null}

      {!loading && mode === "contracts" ? (
        <FormCard title={po ? "Create Contract from PO" : "Contracts"}>
          {po ? (
            <>
              <Select value={selectedPoId} onChange={(event) => setSelectedPoId(event.target.value)}>
                {state.purchaseOrders.map((item) => <option key={item.id} value={item.id}>{item.poNumber}</option>)}
              </Select>
              <MasterSelect label="Contract Document Type" value={contractForm.documentType} items={master("DocumentType")} onChange={(documentType) => setContractForm({ ...contractForm, documentType })} />
              <Button disabled={busy || !po} onClick={() => runAction("Contract created", () => api.contracts.create({ purchaseOrderId: po.id, contractNumber: `CON-${Date.now().toString().slice(-6)}`, title: `Contract for ${po.poNumber}`, documentType: contractForm.documentType, startDateUtc: new Date().toISOString(), endDateUtc: new Date(Date.now() + 365 * 86400000).toISOString() }))}>Create Contract</Button>
            </>
          ) : <EmptyState title="No purchase orders yet" description="Approve an award and generate a PO before creating a contract." />}
          <DataTable data={state.contracts} columns={[
            { key: "contractNumber", label: "Contract" },
            { key: "title", label: "Title" },
            { key: "documentType", label: "Doc Type", render: (item) => String(item.documentType ?? "-") },
            { key: "value", label: "Value", render: (item) => formatCurrency(Number(item.value), String(item.currency)) },
            { key: "status", label: "Status", render: (item) => <StatusBadge status={String(item.status)} /> },
            { key: "id", label: "Open", render: (item) => <Link className="text-brand" href={`/dashboard/contracts/${item.id}`}>Detail</Link> }
          ]} />
        </FormCard>
      ) : null}
    </div>
  );
}

function FormCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <h2 className="text-sm font-semibold text-ink">{title}</h2>
      </CardHeader>
      <CardBody className="space-y-4">{children}</CardBody>
    </Card>
  );
}

function MasterSelect({
  label,
  value,
  items,
  onChange,
  valueMode = "code"
}: {
  label: string;
  value: string;
  items: MasterDataItem[];
  onChange: (value: string) => void;
  valueMode?: "code" | "name";
}) {
  return (
    <div>
      <label className="mb-2 block text-sm font-medium text-slate-700">{label}</label>
      <Select value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="">Select {label.toLowerCase()}</option>
        {items.map((item) => {
          const optionValue = valueMode === "name" ? item.name : item.code;
          return (
            <option key={item.id} value={optionValue}>
              {item.code} - {item.name}
            </option>
          );
        })}
      </Select>
    </div>
  );
}

function PrDetail({ pr, busy, onSubmit, documentTypes }: { pr: PurchaseRequest; busy: boolean; onSubmit: () => void; documentTypes: MasterDataItem[] }) {
  const amount = pr.items.reduce((sum, line) => sum + line.quantity * line.estimatedUnitPrice, 0);
  const documentsEditable = pr.status === "Draft" || pr.status === "Rejected";
  return (
    <div className="space-y-6">
      <Card>
        <CardBody className="space-y-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h2 className="text-lg font-semibold text-ink">{pr.title}</h2>
              <p className="text-sm text-slate-600">{pr.docNum} / DocEntry {pr.docEntry} - {pr.department} - {formatCurrency(amount)}</p>
            </div>
            <StatusBadge status={pr.status} />
          </div>
          <p className="text-sm text-slate-700">{pr.justification}</p>
          <DataTable data={pr.items} columns={[
            { key: "description", label: "Item" },
            { key: "itemCode", label: "Code", render: (item) => String(item.itemCode ?? "-") },
            { key: "unitOfMeasure", label: "Unit", render: (item) => String(item.unitOfMeasure ?? "-") },
            { key: "quantity", label: "Qty" },
            { key: "estimatedUnitPrice", label: "Unit Price", render: (item) => formatCurrency(Number(item.estimatedUnitPrice), pr.currency) }
          ]} />
          {pr.status === "Draft" ? <Button disabled={busy} onClick={onSubmit}>Submit for Approval</Button> : null}
          {pr.status === "Approved" ? <Link className="text-sm font-medium text-brand" href="/dashboard/tenders/create-from-pr">Create tender from this PR</Link> : null}
        </CardBody>
      </Card>
      <DocumentPanel
        entityName="PurchaseRequest"
        entityId={pr.id}
        title={pr.title}
        editable={documentsEditable}
        lockedReason="PurchaseRequestSubmitted"
        documentTypes={documentTypes}
      />
    </div>
  );
}

function TenderDetail({ tender, bids, busy, onPublish }: { tender: Tender; bids: Bid[]; busy: boolean; onPublish: () => void }) {
  return (
    <Card>
      <CardBody className="space-y-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold text-ink">{tender.title}</h2>
            <p className="text-sm text-slate-600">{tender.method} - {tender.visibility} - closes {formatDate(tender.closingDateUtc)}</p>
          </div>
          <StatusBadge status={tender.status} />
        </div>
        <p className="text-sm text-slate-700">{tender.description}</p>
        {tender.status === "Draft" ? <Button disabled={busy} onClick={onPublish}>Publish Tender</Button> : null}
        <BidsTable bids={bids} />
      </CardBody>
    </Card>
  );
}

function BidsTable({ bids }: { bids: Bid[] }) {
  return (
    <DataTable data={bids} columns={[
      { key: "vendorEmail", label: "Vendor" },
      { key: "totalAmount", label: "Amount", render: (item) => formatCurrency(Number(item.totalAmount), String(item.currency)) },
      { key: "status", label: "Status", render: (item) => <StatusBadge status={String(item.status)} /> },
      { key: "tenderClosingDateUtc", label: "Tender Closing", render: (item) => formatDate(String(item.tenderClosingDateUtc)) }
    ]} />
  );
}

function TenderPicker({ tenders, value, onChange }: { tenders: Tender[]; value: string; onChange: (value: string) => void | Promise<void> }) {
  return (
    <Select value={value} onChange={(event) => onChange(event.target.value)}>
      {tenders.map((item) => <option key={item.id} value={item.id}>{item.title}</option>)}
    </Select>
  );
}

function ComparisonTable({ comparisons }: { comparisons: BidComparison[] }) {
  if (!comparisons.length) {
    return <EmptyState title="No bids found" description="Submit vendor bids before comparing offers." />;
  }

  return (
    <DataTable data={comparisons} columns={[
      { key: "vendorEmail", label: "Vendor" },
      { key: "totalAmount", label: "Price", render: (item) => item.totalAmount === null ? <span className="text-slate-500">Hidden until bid opening</span> : <span className={item.isLowest ? "font-semibold text-brand" : ""}>{formatCurrency(Number(item.totalAmount), String(item.currency))}</span> },
      { key: "status", label: "Status", render: (item) => <StatusBadge status={String(item.status)} /> },
      { key: "isLowest", label: "Lowest", render: (item) => item.isLowest ? <span className="rounded-md bg-teal-50 px-2 py-1 text-xs font-semibold text-brand">Lowest bid</span> : <span className="text-slate-500">-</span> }
    ]} />
  );
}
