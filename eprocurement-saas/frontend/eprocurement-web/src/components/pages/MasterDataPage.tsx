"use client";

import { useEffect, useMemo, useState } from "react";
import { Plus, RefreshCw, Settings2, Trash2 } from "lucide-react";
import { api, demoTenantId, MasterDataItem } from "@/lib/api";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { Input } from "@/components/ui/Input";
import { PageHeader } from "@/components/ui/PageHeader";
import { Select } from "@/components/ui/Select";
import { Textarea } from "@/components/ui/Textarea";
import { useToast } from "@/components/ui/ToastProvider";
import { cn } from "@/lib/utils";

const masterTypes = [
  { type: "Department", label: "Departments" },
  { type: "CostCenter", label: "Cost Centers" },
  { type: "Category", label: "Categories" },
  { type: "Item", label: "Items / Materials" },
  { type: "UnitOfMeasure", label: "Units" },
  { type: "ApprovalWorkflow", label: "Approval Workflows" },
  { type: "TenderMethod", label: "Tender Methods" },
  { type: "EvaluationCriteria", label: "Evaluation Criteria" },
  { type: "CommitteeMember", label: "Committee Members" },
  { type: "Currency", label: "Currency" },
  { type: "TaxCode", label: "Tax Codes" },
  { type: "PaymentTerm", label: "Payment Terms" },
  { type: "DeliveryLocation", label: "Delivery Locations" },
  { type: "DocumentType", label: "Document Types" }
];

export function MasterDataPage() {
  const [items, setItems] = useState<MasterDataItem[]>([]);
  const [activeType, setActiveType] = useState(masterTypes[0].type);
  const [form, setForm] = useState({ code: "", name: "", description: "" });
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const { showToast } = useToast();

  const activeLabel = masterTypes.find((item) => item.type === activeType)?.label ?? activeType;
  const visibleItems = useMemo(() => items.filter((item) => item.type === activeType), [activeType, items]);

  async function load() {
    setLoading(true);
    setError("");
    try {
      setItems(await api.masterData.list());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load master data.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, []);

  async function createItem() {
    if (!form.code.trim() || !form.name.trim()) {
      setError("Code and name are required.");
      return;
    }

    setBusy(true);
    setError("");
    try {
      await api.masterData.create({
        tenantId: demoTenantId,
        type: activeType,
        code: form.code,
        name: form.name,
        description: form.description
      });
      setForm({ code: "", name: "", description: "" });
      showToast(`${activeLabel} record created`);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to create master data record.");
    } finally {
      setBusy(false);
    }
  }

  async function toggleActive(item: MasterDataItem) {
    setBusy(true);
    setError("");
    try {
      await api.masterData.update(item.id, {
        code: item.code,
        name: item.name,
        description: item.description,
        isActive: !item.isActive
      });
      showToast(item.isActive ? "Record deactivated" : "Record activated");
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to update record.");
    } finally {
      setBusy(false);
    }
  }

  async function deleteItem(item: MasterDataItem) {
    setBusy(true);
    setError("");
    try {
      await api.masterData.remove(item.id);
      showToast("Record deleted");
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to delete record.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Master Data"
        description="Manage tenant-wide procurement setup used by purchase requests, tenders, evaluation, purchase orders, contracts, and documents."
        action={
          <Button variant="secondary" onClick={load} disabled={loading || busy}>
            <RefreshCw size={16} /> Refresh
          </Button>
        }
      />

      {error ? <div className="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div> : null}

      <div className="grid gap-5 xl:grid-cols-[260px_1fr]">
        <Card className="h-fit">
          <CardHeader>
            <h2 className="flex items-center gap-2 text-sm font-semibold text-ink"><Settings2 size={16} /> Data Sets</h2>
          </CardHeader>
          <CardBody className="space-y-1 p-2">
            {masterTypes.map((item) => {
              const count = items.filter((record) => record.type === item.type).length;
              return (
                <button
                  key={item.type}
                  className={cn(
                    "flex h-10 w-full items-center justify-between rounded-md px-3 text-left text-sm transition",
                    activeType === item.type ? "bg-teal-50 font-medium text-brand" : "text-slate-700 hover:bg-slate-100"
                  )}
                  onClick={() => setActiveType(item.type)}
                  type="button"
                >
                  <span className="truncate">{item.label}</span>
                  <span className="text-xs text-slate-500">{count}</span>
                </button>
              );
            })}
          </CardBody>
        </Card>

        <div className="space-y-5">
          <Card>
            <CardHeader>
              <h2 className="text-sm font-semibold text-ink">Add {activeLabel}</h2>
            </CardHeader>
            <CardBody className="space-y-4">
              <div className="grid gap-3 md:grid-cols-[160px_1fr]">
                <Input placeholder="Code" value={form.code} onChange={(event) => setForm({ ...form, code: event.target.value })} />
                <Input placeholder="Name" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
              </div>
              <Textarea placeholder="Description" value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} />
              <Button disabled={busy || !form.code.trim() || !form.name.trim()} onClick={createItem}>
                <Plus size={16} /> Add Record
              </Button>
            </CardBody>
          </Card>

          {loading ? <div className="rounded-lg border border-line bg-white p-6 text-sm text-slate-500">Loading master data...</div> : null}
          {!loading && !visibleItems.length ? <EmptyState title="No records yet" description={`Add the first ${activeLabel.toLowerCase()} record above.`} /> : null}
          {!loading && visibleItems.length ? (
            <DataTable
              data={visibleItems}
              columns={[
                { key: "code", label: "Code" },
                { key: "name", label: "Name" },
                { key: "description", label: "Description", render: (item) => String(item.description ?? "-") },
                { key: "isActive", label: "Status", render: (item) => item.isActive ? <Badge tone="green">Active</Badge> : <Badge tone="slate">Inactive</Badge> },
                {
                  key: "id",
                  label: "Actions",
                  render: (item) => (
                    <div className="flex items-center gap-2">
                      <Button className="h-8 px-3" variant="secondary" disabled={busy} onClick={() => toggleActive(item as MasterDataItem)}>
                        {item.isActive ? "Deactivate" : "Activate"}
                      </Button>
                      <Button className="h-8 px-3" variant="danger" disabled={busy} onClick={() => deleteItem(item as MasterDataItem)}>
                        <Trash2 size={14} /> Delete
                      </Button>
                    </div>
                  )
                }
              ]}
            />
          ) : null}
        </div>
      </div>
    </div>
  );
}
