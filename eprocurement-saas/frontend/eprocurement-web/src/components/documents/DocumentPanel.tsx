"use client";

import { FilePlus2, Lock, Plus, Save } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { api, demoTenantId, DocumentMetadata, DocumentVersionFilePayload, MasterDataItem } from "@/lib/api";
import { Button } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { StatusBadge } from "@/components/ui/StatusBadge";
import { Textarea } from "@/components/ui/Textarea";
import { useToast } from "@/components/ui/ToastProvider";

const defaultFile: DocumentVersionFilePayload = {
  fileName: "supporting-document.pdf",
  contentType: "application/pdf",
  storageBucket: "eprocurement-documents",
  storageObjectKey: "akpk-demo/documents/supporting-document.pdf",
  sizeBytes: 102400,
  checksumSha256: "replace-with-sha256-checksum",
  changeSummary: "Initial upload",
  virusScanStatus: "Pending"
};

export function DocumentPanel({
  entityName,
  entityId,
  title,
  editable,
  lockedReason,
  documentTypes
}: {
  entityName: string;
  entityId: string;
  title: string;
  editable: boolean;
  lockedReason: string;
  documentTypes: MasterDataItem[];
}) {
  const { showToast } = useToast();
  const [documents, setDocuments] = useState<DocumentMetadata[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [metadata, setMetadata] = useState({ title: `${title} Attachment`, documentType: documentTypes[0]?.code ?? "GENERAL", status: "Draft" });
  const [file, setFile] = useState<DocumentVersionFilePayload>(defaultFile);

  const selected = useMemo(() => documents.find((item) => item.id === selectedId) ?? documents[0], [documents, selectedId]);
  const canEditSelected = Boolean(editable && selected && !selected.isLocked && selected.status !== "Locked" && selected.status !== "Archived");

  useEffect(() => {
    void load();
  }, [entityName, entityId]);

  useEffect(() => {
    setMetadata((current) => ({
      ...current,
      title: current.title || `${title} Attachment`,
      documentType: current.documentType || documentTypes[0]?.code || "GENERAL"
    }));
  }, [documentTypes, title]);

  async function load() {
    setBusy(true);
    setError("");
    try {
      const items = await api.documents.list({ entityName, entityId });
      setDocuments(items);
      if (items[0]) setSelectedId((current) => current || items[0].id);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load documents.");
    } finally {
      setBusy(false);
    }
  }

  async function runAction(message: string, action: () => Promise<unknown>) {
    setBusy(true);
    setError("");
    try {
      await action();
      showToast(message);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Document action failed.");
    } finally {
      setBusy(false);
    }
  }

  function updateFile<K extends keyof DocumentVersionFilePayload>(key: K, value: DocumentVersionFilePayload[K]) {
    setFile((current) => ({ ...current, [key]: value }));
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <h2 className="text-sm font-semibold text-ink">Documents</h2>
            <p className="mt-1 text-sm text-slate-600">{editable ? "Add or revise supporting documents before this record is submitted." : "Documents are view only because this workflow stage is locked."}</p>
          </div>
          <Button variant="secondary" disabled={busy} onClick={load}>Refresh</Button>
        </div>
      </CardHeader>
      <CardBody className="space-y-5">
        {error ? <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div> : null}

        <DataTable
          data={documents as unknown as Record<string, unknown>[]}
          searchPlaceholder="Search linked documents"
          columns={[
            { key: "title", label: "Document", render: (item) => <button className="font-medium text-brand" onClick={() => setSelectedId(String(item.id))}>{String(item.title)}</button> },
            { key: "documentType", label: "Type" },
            { key: "currentVersionNumber", label: "Version", render: (item) => `v${String(item.currentVersionNumber)}` },
            { key: "createdByEmail", label: "Owner" },
            { key: "status", label: "Status", render: (item) => <StatusBadge status={String(item.status)} /> }
          ]}
        />

        {!documents.length ? <EmptyState title="No documents linked" description="Create the first document from this transaction screen." /> : null}

        {editable ? (
          <div className="grid gap-4 xl:grid-cols-2">
            <div className="space-y-3 rounded-md border border-line bg-slate-50 p-4">
              <div className="flex items-center gap-2 text-sm font-semibold text-ink">
                <FilePlus2 size={16} className="text-brand" /> Document Header
              </div>
              <Field label="Document Title">
                <Input value={metadata.title} onChange={(event) => setMetadata({ ...metadata, title: event.target.value })} />
              </Field>
              <Field label="Document Type">
                <Select value={metadata.documentType} onChange={(event) => setMetadata({ ...metadata, documentType: event.target.value })}>
                  {documentTypes.length === 0 ? <option value={metadata.documentType}>{metadata.documentType}</option> : null}
                  {documentTypes.map((item) => <option key={item.id} value={item.code}>{item.code} - {item.name}</option>)}
                </Select>
              </Field>
              <Field label="Document Status">
                <Select value={metadata.status} onChange={(event) => setMetadata({ ...metadata, status: event.target.value })}>
                  <option value="Draft">Draft</option>
                  <option value="Submitted">Submitted</option>
                  <option value="Archived">Archived</option>
                </Select>
              </Field>
              <div className="flex flex-wrap gap-2">
                <Button
                  disabled={busy || !metadata.title || !file.fileName}
                  onClick={() => runAction("Document created", () => api.documents.create({ tenantId: demoTenantId, entityName, entityId, documentType: metadata.documentType, title: metadata.title, file }))}
                >
                  <Plus size={16} />Create
                </Button>
                {canEditSelected && selected ? (
                  <Button
                    variant="secondary"
                    disabled={busy || !metadata.title}
                    onClick={() => runAction("Document metadata updated", () => api.documents.updateMetadata(selected.id, metadata))}
                  >
                    <Save size={16} />Update Metadata
                  </Button>
                ) : null}
              </div>
            </div>

            <div className="space-y-3 rounded-md border border-line bg-slate-50 p-4">
              <div className="flex items-center gap-2 text-sm font-semibold text-ink">
                <FilePlus2 size={16} className="text-brand" /> Version File Metadata
              </div>
              <FileFields file={file} updateFile={updateFile} />
              <div className="flex flex-wrap gap-2">
                {canEditSelected && selected ? (
                  <Button disabled={busy || !file.fileName} onClick={() => runAction("New document version saved", () => api.documents.addVersion(selected.id, file))}>
                    <Plus size={16} />Add Version
                  </Button>
                ) : null}
                {selected && !selected.isLocked ? (
                  <Button variant="secondary" disabled={busy} onClick={() => runAction("Document locked", () => api.documents.lock(selected.id, lockedReason))}>
                    <Lock size={16} />Lock
                  </Button>
                ) : null}
              </div>
            </div>
          </div>
        ) : null}

        {selected ? (
          <div className="space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h3 className="text-sm font-semibold text-ink">Versions for {selected.title}</h3>
              {selected.isLocked ? <span className="text-sm text-slate-500">Locked: {selected.lockedReason ?? "Workflow locked"}</span> : null}
            </div>
            <DataTable
              data={(selected.versions ?? []) as unknown as Record<string, unknown>[]}
              searchPlaceholder="Search versions"
              columns={[
                { key: "versionNumber", label: "Version", render: (item) => `v${String(item.versionNumber)}` },
                { key: "fileName", label: "File" },
                { key: "storageObjectKey", label: "MinIO Object" },
                { key: "checksumSha256", label: "SHA-256" },
                { key: "changeSummary", label: "Change Summary" },
                { key: "virusScanStatus", label: "Scan" }
              ]}
            />
          </div>
        ) : null}
      </CardBody>
    </Card>
  );
}

function FileFields({ file, updateFile }: { file: DocumentVersionFilePayload; updateFile: <K extends keyof DocumentVersionFilePayload>(key: K, value: DocumentVersionFilePayload[K]) => void }) {
  return (
    <div className="space-y-3">
      <Field label="File Name">
        <Input value={file.fileName} onChange={(event) => updateFile("fileName", event.target.value)} />
      </Field>
      <Field label="Content Type">
        <Input value={file.contentType} onChange={(event) => updateFile("contentType", event.target.value)} />
      </Field>
      <Field label="MinIO Bucket">
        <Input value={file.storageBucket} onChange={(event) => updateFile("storageBucket", event.target.value)} />
      </Field>
      <Field label="MinIO Object Key">
        <Input value={file.storageObjectKey} onChange={(event) => updateFile("storageObjectKey", event.target.value)} />
      </Field>
      <Field label="Size Bytes">
        <Input type="number" min={1} value={file.sizeBytes} onChange={(event) => updateFile("sizeBytes", Number(event.target.value))} />
      </Field>
      <Field label="SHA-256 Checksum">
        <Input value={file.checksumSha256} onChange={(event) => updateFile("checksumSha256", event.target.value)} />
      </Field>
      <Field label="Change Summary">
        <Textarea value={file.changeSummary} onChange={(event) => updateFile("changeSummary", event.target.value)} />
      </Field>
      <Field label="Virus Scan Status">
        <Select value={file.virusScanStatus} onChange={(event) => updateFile("virusScanStatus", event.target.value)}>
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
