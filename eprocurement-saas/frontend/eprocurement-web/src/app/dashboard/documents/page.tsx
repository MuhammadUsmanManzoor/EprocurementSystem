"use client";

import { Archive, FilePlus2, Lock, Plus, RefreshCcw } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { Button } from "@/components/ui/Button";
import { Card } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { Input } from "@/components/ui/Input";
import { PageHeader } from "@/components/ui/PageHeader";
import { Select } from "@/components/ui/Select";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Textarea } from "@/components/ui/Textarea";
import { useToast } from "@/components/ui/ToastProvider";
import { api, demoTenantId, demoTenderId, DocumentMetadata, DocumentVersionFilePayload, MasterDataItem } from "@/lib/api";

const emptyEntityId = demoTenderId;

const initialFileForm: DocumentVersionFilePayload = {
  fileName: "tender-specification.pdf",
  contentType: "application/pdf",
  storageBucket: "eprocurement-documents",
  storageObjectKey: "akpk-demo/tenders/tender-specification-v1.pdf",
  sizeBytes: 102400,
  checksumSha256: "replace-with-sha256-checksum",
  changeSummary: "Initial upload",
  virusScanStatus: "Pending"
};

export default function DocumentsPage() {
  const { showToast } = useToast();
  const [documents, setDocuments] = useState<DocumentMetadata[]>([]);
  const [masterData, setMasterData] = useState<MasterDataItem[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({
    entityName: "Tender",
    entityId: emptyEntityId,
    documentType: "RFQ-DOC",
    title: "Tender Specification"
  });
  const [fileForm, setFileForm] = useState<DocumentVersionFilePayload>(initialFileForm);

  const selected = useMemo(() => documents.find((document) => document.id === selectedId) ?? documents[0], [documents, selectedId]);
  const documentTypes = masterData.filter((item) => item.type === "DocumentType" && item.isActive);
  const canEditSelected = selected && !selected.isLocked && selected.status !== "Locked" && selected.status !== "Archived";

  useEffect(() => {
    void load();
  }, []);

  async function load() {
    setBusy(true);
    setError("");
    try {
      const [items, master] = await Promise.all([api.documents.list(), api.masterData.list()]);
      setDocuments(items);
      setMasterData(master);
      if (items[0]) setSelectedId((current) => current || items[0].id);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load documents.");
    } finally {
      setBusy(false);
    }
  }

  async function runAction(successMessage: string, action: () => Promise<unknown>) {
    setBusy(true);
    setError("");
    try {
      await action();
      showToast(successMessage);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Document action failed.");
    } finally {
      setBusy(false);
    }
  }

  function updateFile<K extends keyof DocumentVersionFilePayload>(key: K, value: DocumentVersionFilePayload[K]) {
    setFileForm((current) => ({ ...current, [key]: value }));
  }

  return (
    <div>
      <PageHeader
        title="Documents"
        description="Create document headers, save MinIO file metadata, keep versions, and lock records when the parent workflow is no longer editable."
        action={<Button variant="secondary" onClick={load} disabled={busy}><RefreshCcw size={16} />Refresh</Button>}
      />

      {error ? <div className="mb-4 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div> : null}

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_420px]">
        <div className="space-y-6">
          <DataTable
            data={documents as unknown as Record<string, unknown>[]}
            searchPlaceholder="Search documents"
            columns={[
              { key: "title", label: "Document", render: (item) => <button className="font-medium text-brand" onClick={() => setSelectedId(String(item.id))}>{String(item.title)}</button> },
              { key: "entityName", label: "Linked Entity" },
              { key: "documentType", label: "Type" },
              { key: "currentVersionNumber", label: "Current Version", render: (item) => `v${String(item.currentVersionNumber)}` },
              { key: "createdByEmail", label: "Owner" },
              { key: "status", label: "Status", render: (item) => <StatusBadge status={String(item.status)} /> }
            ]}
          />

          {selected ? (
            <Card className="p-5">
              <div className="mb-4 flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <h2 className="text-base font-semibold text-ink">{selected.title}</h2>
                  <p className="mt-1 text-sm text-slate-600">{selected.entityName} / {selected.entityId}</p>
                </div>
                <div className="flex gap-2">
                  {canEditSelected ? (
                    <Button
                      variant="secondary"
                      disabled={busy}
                      onClick={() => runAction("New document version saved", () => api.documents.addVersion(selected.id, fileForm))}
                    >
                      <Plus size={16} />Add Version
                    </Button>
                  ) : null}
                  {!selected.isLocked ? (
                    <Button
                      variant="secondary"
                      disabled={busy}
                      onClick={() => runAction("Document locked", () => api.documents.lock(selected.id, `${selected.entityName} workflow locked`))}
                    >
                      <Lock size={16} />Lock
                    </Button>
                  ) : null}
                </div>
              </div>

              <DataTable
                data={(selected.versions ?? []) as unknown as Record<string, unknown>[]}
                searchPlaceholder="Search versions"
                columns={[
                  { key: "versionNumber", label: "Version", render: (item) => `v${String(item.versionNumber)}` },
                  { key: "fileName", label: "File" },
                  { key: "storageObjectKey", label: "MinIO Object" },
                  { key: "checksumSha256", label: "Checksum" },
                  { key: "uploadedByEmail", label: "Uploaded By" },
                  { key: "virusScanStatus", label: "Scan" }
                ]}
              />
            </Card>
          ) : null}
        </div>

        <div className="space-y-6">
          <Card className="p-5">
            <div className="mb-4 flex items-center gap-2">
              <FilePlus2 size={18} className="text-brand" />
              <h2 className="text-base font-semibold text-ink">Create Document</h2>
            </div>
            <div className="space-y-3">
              <Field label="Title">
                <Input value={form.title} onChange={(event) => setForm({ ...form, title: event.target.value })} />
              </Field>
              <Field label="Document Type">
                <Select value={form.documentType} onChange={(event) => setForm({ ...form, documentType: event.target.value })}>
                  {documentTypes.length === 0 ? <option value={form.documentType}>{form.documentType}</option> : null}
                  {documentTypes.map((item) => <option key={item.id} value={item.code}>{item.name}</option>)}
                </Select>
              </Field>
              <Field label="Linked Entity">
                <Select value={form.entityName} onChange={(event) => setForm({ ...form, entityName: event.target.value })}>
                  <option value="PurchaseRequest">Purchase Request</option>
                  <option value="Tender">Tender</option>
                  <option value="Bid">Bid</option>
                  <option value="AwardDecision">Award Decision</option>
                  <option value="PurchaseOrder">Purchase Order</option>
                  <option value="Contract">Contract</option>
                </Select>
              </Field>
              <Field label="Linked Entity ID">
                <Input value={form.entityId} onChange={(event) => setForm({ ...form, entityId: event.target.value })} />
              </Field>
              <FileFields fileForm={fileForm} updateFile={updateFile} />
              <Button
                className="w-full"
                disabled={busy || !form.title || !form.entityId || !fileForm.fileName}
                onClick={() => runAction("Document created with version 1", () => api.documents.create({ tenantId: demoTenantId, ...form, file: fileForm }))}
              >
                Create Document
              </Button>
            </div>
          </Card>

          <Card className="p-5">
            <div className="mb-4 flex items-center gap-2">
              <Archive size={18} className="text-brand" />
              <h2 className="text-base font-semibold text-ink">Version Control Saved</h2>
            </div>
            <div className="space-y-2 text-sm text-slate-600">
              <p>Header: tenant, linked entity, type, title, status, current version, lock fields, creator.</p>
              <p>Version: file name, content type, MinIO bucket/key, size, SHA-256 checksum, change summary, uploader, upload date, current flag, scan status.</p>
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
}

function FileFields({ fileForm, updateFile }: { fileForm: DocumentVersionFilePayload; updateFile: <K extends keyof DocumentVersionFilePayload>(key: K, value: DocumentVersionFilePayload[K]) => void }) {
  return (
    <div className="space-y-3 rounded-md border border-line bg-slate-50 p-3">
      <Field label="File Name">
        <Input value={fileForm.fileName} onChange={(event) => updateFile("fileName", event.target.value)} />
      </Field>
      <Field label="Content Type">
        <Input value={fileForm.contentType} onChange={(event) => updateFile("contentType", event.target.value)} />
      </Field>
      <Field label="MinIO Bucket">
        <Input value={fileForm.storageBucket} onChange={(event) => updateFile("storageBucket", event.target.value)} />
      </Field>
      <Field label="MinIO Object Key">
        <Input value={fileForm.storageObjectKey} onChange={(event) => updateFile("storageObjectKey", event.target.value)} />
      </Field>
      <Field label="Size Bytes">
        <Input type="number" min={1} value={fileForm.sizeBytes} onChange={(event) => updateFile("sizeBytes", Number(event.target.value))} />
      </Field>
      <Field label="SHA-256 Checksum">
        <Input value={fileForm.checksumSha256} onChange={(event) => updateFile("checksumSha256", event.target.value)} />
      </Field>
      <Field label="Change Summary">
        <Textarea value={fileForm.changeSummary} onChange={(event) => updateFile("changeSummary", event.target.value)} />
      </Field>
      <Field label="Virus Scan Status">
        <Select value={fileForm.virusScanStatus} onChange={(event) => updateFile("virusScanStatus", event.target.value)}>
          <option value="Pending">Pending</option>
          <option value="Clean">Clean</option>
          <option value="Infected">Infected</option>
          <option value="Failed">Failed</option>
        </Select>
      </Field>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-medium uppercase tracking-normal text-slate-500">{label}</span>
      {children}
    </label>
  );
}
