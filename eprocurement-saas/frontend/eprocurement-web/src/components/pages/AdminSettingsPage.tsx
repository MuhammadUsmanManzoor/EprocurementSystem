"use client";

import { useEffect, useMemo, useState } from "react";
import { KeyRound, Plus, RefreshCw, ShieldCheck, Workflow, Users } from "lucide-react";
import { api, ApprovalMatrixRule, demoTenantId, RoleAdmin, RolePermission, SaveApprovalMatrixRule, UserAdmin } from "@/lib/api";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { Input } from "@/components/ui/Input";
import { PageHeader } from "@/components/ui/PageHeader";
import { Select } from "@/components/ui/Select";
import { Textarea } from "@/components/ui/Textarea";
import { useToast } from "@/components/ui/ToastProvider";
import { MasterDataPage } from "@/components/pages/MasterDataPage";
import { cn } from "@/lib/utils";

type Tab = "master-data" | "users" | "roles" | "approval-matrix";

const permissionActions: Array<{ key: keyof RolePermission; label: string }> = [
  { key: "canView", label: "View" },
  { key: "canCreate", label: "Create" },
  { key: "canEdit", label: "Edit" },
  { key: "canDelete", label: "Delete" },
  { key: "canSubmit", label: "Submit" },
  { key: "canApprove", label: "Approve" },
  { key: "canOpen", label: "Open" },
  { key: "canEvaluate", label: "Evaluate" },
  { key: "canAward", label: "Award" },
  { key: "canGenerate", label: "Generate" },
  { key: "canExport", label: "Export" },
  { key: "canAudit", label: "Audit" }
];

export function AdminSettingsPage() {
  const [tab, setTab] = useState<Tab>("master-data");

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap gap-2">
        <TabButton active={tab === "master-data"} onClick={() => setTab("master-data")}>Master Data</TabButton>
        <TabButton active={tab === "users"} onClick={() => setTab("users")}>Users</TabButton>
        <TabButton active={tab === "approval-matrix"} onClick={() => setTab("approval-matrix")}>Approval Matrix</TabButton>
        <TabButton active={tab === "roles"} onClick={() => setTab("roles")}>Roles & Permissions</TabButton>
      </div>
      {tab === "master-data" ? <MasterDataPage /> : null}
      {tab === "users" ? <UsersPanel /> : null}
      {tab === "approval-matrix" ? <ApprovalMatrixPanel /> : null}
      {tab === "roles" ? <RolesPanel /> : null}
    </div>
  );
}

function TabButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      className={cn("h-10 rounded-md border px-4 text-sm font-medium", active ? "border-brand bg-teal-50 text-brand" : "border-line bg-white text-slate-700 hover:bg-slate-50")}
      onClick={onClick}
      type="button"
    >
      {children}
    </button>
  );
}

function UsersPanel() {
  const [users, setUsers] = useState<UserAdmin[]>([]);
  const [roles, setRoles] = useState<RoleAdmin[]>([]);
  const [selected, setSelected] = useState<UserAdmin | null>(null);
  const [form, setForm] = useState({ username: "", email: "", fullName: "", role: "", password: "" });
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const { showToast } = useToast();

  async function load() {
    setLoading(true);
    setError("");
    try {
      const [nextUsers, nextRoles] = await Promise.all([api.users.list(), api.roles.list()]);
      setUsers(nextUsers);
      setRoles(nextRoles.filter((role) => role.isActive));
      setForm((current) => ({ ...current, role: current.role || nextRoles.find((role) => role.isActive)?.code || "" }));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load users.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, []);

  function editUser(user: UserAdmin) {
    setSelected(user);
    setForm({ username: user.username, email: user.email, fullName: user.fullName, role: user.role, password: "" });
  }

  async function saveUser() {
    if (!form.username.trim() || !form.fullName.trim() || !form.role || (!selected && !form.email.trim())) {
      setError("Username, email, full name, and role are required.");
      return;
    }

    setBusy(true);
    setError("");
    try {
      if (selected) {
        await api.users.update(selected.id, { tenantId: selected.tenantId ?? demoTenantId, username: form.username, fullName: form.fullName, role: form.role, isActive: selected.isActive, password: form.password || undefined });
        showToast("User updated");
      } else {
        await api.users.create({ tenantId: demoTenantId, username: form.username, email: form.email, fullName: form.fullName, role: form.role, password: form.password || "Password123!" });
        showToast("User created");
      }
      setSelected(null);
      setForm({ username: "", email: "", fullName: "", role: roles[0]?.code ?? "", password: "" });
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to save user.");
    } finally {
      setBusy(false);
    }
  }

  async function toggleUser(user: UserAdmin) {
    setBusy(true);
    setError("");
    try {
      await api.users.update(user.id, { tenantId: user.tenantId ?? demoTenantId, username: user.username, fullName: user.fullName, role: user.role, isActive: !user.isActive });
      showToast(user.isActive ? "User deactivated" : "User activated");
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to update user.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-5">
      <PageHeader title="User Master Data" description="Manage users that can be used for login, workflow assignment, approval stages, audit ownership, and role-based access." action={<Button variant="secondary" onClick={load} disabled={loading || busy}><RefreshCw size={16} /> Refresh</Button>} />
      {error ? <div className="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div> : null}
      <Card>
        <CardHeader><h2 className="flex items-center gap-2 text-sm font-semibold text-ink"><Users size={16} /> {selected ? "Edit User" : "Add User"}</h2></CardHeader>
        <CardBody className="grid gap-3 lg:grid-cols-[160px_1fr_1fr_180px_180px_auto]">
          <Input placeholder="Username" value={form.username} onChange={(event) => setForm({ ...form, username: event.target.value })} />
          <Input placeholder="Email" disabled={Boolean(selected)} value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} />
          <Input placeholder="Full name" value={form.fullName} onChange={(event) => setForm({ ...form, fullName: event.target.value })} />
          <Select value={form.role} onChange={(event) => setForm({ ...form, role: event.target.value })}>
            {roles.map((role) => <option key={role.id} value={role.code}>{role.name}</option>)}
          </Select>
          <Input placeholder={selected ? "New password optional" : "Password"} value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} />
          <Button disabled={busy} onClick={saveUser}><Plus size={16} /> {selected ? "Save" : "Add"}</Button>
        </CardBody>
      </Card>
      {loading ? <div className="rounded-lg border border-line bg-white p-6 text-sm text-slate-500">Loading users...</div> : (
        <DataTable data={users} columns={[
          { key: "username", label: "Username" },
          { key: "email", label: "Email" },
          { key: "fullName", label: "Full Name" },
          { key: "role", label: "Role" },
          { key: "isActive", label: "Status", render: (item) => item.isActive ? <Badge tone="green">Active</Badge> : <Badge tone="slate">Inactive</Badge> },
          { key: "id", label: "Actions", render: (item) => <div className="flex gap-2"><Button className="h-8 px-3" variant="secondary" onClick={() => editUser(item as UserAdmin)}>Edit</Button><Button className="h-8 px-3" variant="secondary" onClick={() => toggleUser(item as UserAdmin)}>{item.isActive ? "Deactivate" : "Activate"}</Button></div> }
        ]} />
      )}
    </div>
  );
}

type MatrixDraftStage = {
  stageOrder: number;
  stageName: string;
  approversText: string;
};

type MatrixDraft = {
  id?: string;
  name: string;
  description: string;
  minAmount: string;
  maxAmount: string;
  department: string;
  costCenter: string;
  category: string;
  priority: string;
  isActive: boolean;
  stages: MatrixDraftStage[];
};

const emptyMatrixDraft: MatrixDraft = {
  name: "",
  description: "",
  minAmount: "",
  maxAmount: "",
  department: "",
  costCenter: "",
  category: "",
  priority: "100",
  isActive: true,
  stages: [
    { stageOrder: 1, stageName: "Stage 1 - Department Approval", approversText: "approver@akpk.com" },
    { stageOrder: 2, stageName: "Stage 2 - Finance Review", approversText: "finance@akpk.com, procurement@akpk.com" }
  ]
};

function ApprovalMatrixPanel() {
  const [rules, setRules] = useState<ApprovalMatrixRule[]>([]);
  const [users, setUsers] = useState<UserAdmin[]>([]);
  const [draft, setDraft] = useState<MatrixDraft>(emptyMatrixDraft);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const { showToast } = useToast();

  async function load() {
    setLoading(true);
    setError("");
    try {
      const [nextRules, nextUsers] = await Promise.all([api.approvalMatrix.list(), api.users.list()]);
      setRules(nextRules);
      setUsers(nextUsers.filter((user) => user.isActive));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load approval matrix.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, []);

  function editRule(rule: ApprovalMatrixRule) {
    setDraft({
      id: rule.id,
      name: rule.name,
      description: rule.description ?? "",
      minAmount: rule.minAmount == null ? "" : String(rule.minAmount),
      maxAmount: rule.maxAmount == null ? "" : String(rule.maxAmount),
      department: rule.department ?? "",
      costCenter: rule.costCenter ?? "",
      category: rule.category ?? "",
      priority: String(rule.priority),
      isActive: rule.isActive,
      stages: [...rule.stages]
        .sort((a, b) => a.stageOrder - b.stageOrder)
        .map((stage) => ({
          stageOrder: stage.stageOrder,
          stageName: stage.stageName,
          approversText: stage.approvers.map((approver) => approver.approverEmail).join(", ")
        }))
    });
  }

  function resetDraft() {
    setDraft({ ...emptyMatrixDraft, stages: emptyMatrixDraft.stages.map((stage) => ({ ...stage })) });
  }

  function updateStage(index: number, patch: Partial<MatrixDraftStage>) {
    setDraft({
      ...draft,
      stages: draft.stages.map((stage, stageIndex) => stageIndex === index ? { ...stage, ...patch } : stage)
    });
  }

  function addStage() {
    const nextOrder = Math.max(0, ...draft.stages.map((stage) => stage.stageOrder)) + 1;
    setDraft({
      ...draft,
      stages: [...draft.stages, { stageOrder: nextOrder, stageName: `Stage ${nextOrder}`, approversText: "" }]
    });
  }

  function removeStage(index: number) {
    setDraft({ ...draft, stages: draft.stages.filter((_, stageIndex) => stageIndex !== index) });
  }

  function toPayload(): SaveApprovalMatrixRule | null {
    if (!draft.name.trim()) {
      setError("Rule name is required.");
      return null;
    }

    const stages = draft.stages
      .map((stage) => ({
        stageOrder: Number(stage.stageOrder),
        stageName: stage.stageName.trim(),
        approverEmails: stage.approversText.split(",").map((email) => email.trim()).filter(Boolean)
      }))
      .filter((stage) => stage.stageName);

    if (!stages.length || stages.some((stage) => !stage.approverEmails.length)) {
      setError("Each approval stage must have a name and at least one approver.");
      return null;
    }

    return {
      tenantId: demoTenantId,
      name: draft.name.trim(),
      description: draft.description.trim() || undefined,
      minAmount: draft.minAmount ? Number(draft.minAmount) : null,
      maxAmount: draft.maxAmount ? Number(draft.maxAmount) : null,
      department: draft.department.trim() || null,
      costCenter: draft.costCenter.trim() || null,
      category: draft.category.trim() || null,
      priority: Number(draft.priority || 100),
      isActive: draft.isActive,
      stages
    };
  }

  async function saveRule() {
    const payload = toPayload();
    if (!payload) return;

    setBusy(true);
    setError("");
    try {
      if (draft.id) {
        await api.approvalMatrix.update(draft.id, payload);
        showToast("Approval matrix updated");
      } else {
        await api.approvalMatrix.create(payload);
        showToast("Approval matrix created");
      }
      resetDraft();
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to save approval matrix.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-5">
      <PageHeader
        title="Approval Matrix"
        description="Configure vertical approval stages and horizontal approvers by amount, department, cost center, and category."
        action={<Button variant="secondary" onClick={load} disabled={loading || busy}><RefreshCw size={16} /> Refresh</Button>}
      />
      {error ? <div className="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div> : null}

      <div className="grid gap-5 xl:grid-cols-[420px_1fr]">
        <Card className="h-fit">
          <CardHeader><h2 className="flex items-center gap-2 text-sm font-semibold text-ink"><Workflow size={16} /> {draft.id ? "Edit Matrix Rule" : "Add Matrix Rule"}</h2></CardHeader>
          <CardBody className="space-y-3">
            <Input placeholder="Rule name" value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} />
            <Textarea placeholder="Description" value={draft.description} onChange={(event) => setDraft({ ...draft, description: event.target.value })} />
            <div className="grid gap-3 sm:grid-cols-2">
              <Input type="number" placeholder="Min amount" value={draft.minAmount} onChange={(event) => setDraft({ ...draft, minAmount: event.target.value })} />
              <Input type="number" placeholder="Max amount" value={draft.maxAmount} onChange={(event) => setDraft({ ...draft, maxAmount: event.target.value })} />
              <Input placeholder="Department" value={draft.department} onChange={(event) => setDraft({ ...draft, department: event.target.value })} />
              <Input placeholder="Cost center" value={draft.costCenter} onChange={(event) => setDraft({ ...draft, costCenter: event.target.value })} />
              <Input placeholder="Category" value={draft.category} onChange={(event) => setDraft({ ...draft, category: event.target.value })} />
              <Input type="number" placeholder="Priority" value={draft.priority} onChange={(event) => setDraft({ ...draft, priority: event.target.value })} />
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" checked={draft.isActive} onChange={() => setDraft({ ...draft, isActive: !draft.isActive })} />
              Active rule
            </label>

            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-semibold text-ink">Stages</h3>
                <Button className="h-8 px-3" variant="secondary" onClick={addStage}><Plus size={14} /> Stage</Button>
              </div>
              {draft.stages.map((stage, index) => (
                <div key={index} className="space-y-2 rounded-md border border-line bg-slate-50 p-3">
                  <div className="grid gap-2 sm:grid-cols-[80px_1fr_auto]">
                    <Input type="number" value={stage.stageOrder} onChange={(event) => updateStage(index, { stageOrder: Number(event.target.value) })} />
                    <Input placeholder="Stage name" value={stage.stageName} onChange={(event) => updateStage(index, { stageName: event.target.value })} />
                    <Button className="h-10 px-3" variant="secondary" onClick={() => removeStage(index)}>Remove</Button>
                  </div>
                  <Textarea
                    placeholder="Approver emails, comma separated"
                    value={stage.approversText}
                    onChange={(event) => updateStage(index, { approversText: event.target.value })}
                  />
                </div>
              ))}
            </div>

            <div className="rounded-md border border-line bg-white p-3 text-xs text-slate-600">
              Active approvers available: {users.map((user) => user.email).join(", ") || "No users loaded"}
            </div>

            <div className="flex flex-wrap gap-2">
              <Button disabled={busy} onClick={saveRule}><ShieldCheck size={16} /> {draft.id ? "Save Rule" : "Create Rule"}</Button>
              <Button variant="secondary" disabled={busy} onClick={resetDraft}>New Rule</Button>
            </div>
          </CardBody>
        </Card>

        <div className="space-y-4">
          {loading ? <div className="rounded-lg border border-line bg-white p-6 text-sm text-slate-500">Loading approval matrix...</div> : null}
          {!loading && rules.map((rule) => (
            <Card key={rule.id}>
              <CardHeader className="flex flex-row items-start justify-between gap-3">
                <div>
                  <h2 className="text-sm font-semibold text-ink">{rule.name}</h2>
                  <p className="mt-1 text-xs text-slate-500">{rule.description || "No description"}</p>
                </div>
                <div className="flex items-center gap-2">
                  <Badge tone={rule.isActive ? "green" : "slate"}>{rule.isActive ? "Active" : "Inactive"}</Badge>
                  <Button className="h-8 px-3" variant="secondary" onClick={() => editRule(rule)}>Edit</Button>
                </div>
              </CardHeader>
              <CardBody className="space-y-3">
                <div className="grid gap-2 text-xs text-slate-600 md:grid-cols-5">
                  <span>Amount: {rule.minAmount ?? "Any"} - {rule.maxAmount ?? "Any"}</span>
                  <span>Department: {rule.department || "Any"}</span>
                  <span>Cost Center: {rule.costCenter || "Any"}</span>
                  <span>Category: {rule.category || "Any"}</span>
                  <span>Priority: {rule.priority}</span>
                </div>
                <div className="space-y-2">
                  {[...rule.stages].sort((a, b) => a.stageOrder - b.stageOrder).map((stage) => (
                    <div key={stage.id} className="rounded-md border border-line bg-slate-50 p-3">
                      <div className="text-sm font-medium text-ink">{stage.stageOrder}. {stage.stageName}</div>
                      <div className="mt-1 text-xs text-slate-600">Any one can approve: {stage.approvers.map((approver) => approver.approverEmail).join(", ")}</div>
                    </div>
                  ))}
                </div>
              </CardBody>
            </Card>
          ))}
        </div>
      </div>
    </div>
  );
}

function RolesPanel() {
  const [roles, setRoles] = useState<RoleAdmin[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [draft, setDraft] = useState<RoleAdmin | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const { showToast } = useToast();

  const selected = useMemo(() => roles.find((role) => role.id === selectedId) ?? roles[0], [roles, selectedId]);
  const grouped = useMemo(() => {
    const source = draft?.permissions ?? [];
    return source.reduce<Record<string, RolePermission[]>>((groups, permission) => {
      groups[permission.module] = [...(groups[permission.module] ?? []), permission];
      return groups;
    }, {});
  }, [draft]);

  async function load() {
    setLoading(true);
    setError("");
    try {
      const nextRoles = await api.roles.list();
      setRoles(nextRoles);
      const nextSelected = nextRoles.find((role) => role.id === selectedId) ?? nextRoles[0] ?? null;
      setSelectedId(nextSelected?.id ?? "");
      setDraft(nextSelected ? structuredClone(nextSelected) : null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load roles.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, []);

  function selectRole(id: string) {
    const role = roles.find((item) => item.id === id) ?? null;
    setSelectedId(id);
    setDraft(role ? structuredClone(role) : null);
  }

  function toggle(permissionId: string, key: keyof RolePermission) {
    if (!draft) return;
    setDraft({
      ...draft,
      permissions: draft.permissions.map((permission) => permission.id === permissionId ? { ...permission, [key]: !permission[key] } : permission)
    });
  }

  async function saveRole() {
    if (!draft) return;
    setBusy(true);
    setError("");
    try {
      await api.roles.update(draft.id, { name: draft.name, description: draft.description, isActive: draft.isActive, permissions: draft.permissions });
      showToast("Role permissions updated");
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to save role.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-5">
      <PageHeader title="Roles & Permissions" description="Manage role-wise access for every procurement scenario and assign these roles to users." action={<Button variant="secondary" onClick={load} disabled={loading || busy}><RefreshCw size={16} /> Refresh</Button>} />
      {error ? <div className="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div> : null}
      {loading ? <div className="rounded-lg border border-line bg-white p-6 text-sm text-slate-500">Loading roles...</div> : null}
      {!loading && draft ? (
        <div className="grid gap-5 xl:grid-cols-[320px_1fr]">
          <Card className="h-fit">
            <CardHeader><h2 className="flex items-center gap-2 text-sm font-semibold text-ink"><ShieldCheck size={16} /> Roles</h2></CardHeader>
            <CardBody className="space-y-3">
              <Select value={selected?.id ?? ""} onChange={(event) => selectRole(event.target.value)}>
                {roles.map((role) => <option key={role.id} value={role.id}>{role.name}</option>)}
              </Select>
              <Input value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} />
              <Textarea value={draft.description} onChange={(event) => setDraft({ ...draft, description: event.target.value })} />
              <label className="flex items-center gap-2 text-sm text-slate-700">
                <input type="checkbox" checked={draft.isActive} onChange={() => setDraft({ ...draft, isActive: !draft.isActive })} />
                Active role
              </label>
              <Button disabled={busy} onClick={saveRole}><KeyRound size={16} /> Save Permissions</Button>
            </CardBody>
          </Card>

          <div className="space-y-4">
            {Object.entries(grouped).map(([module, permissions]) => (
              <Card key={module}>
                <CardHeader><h2 className="text-sm font-semibold text-ink">{module}</h2></CardHeader>
                <CardBody className="overflow-x-auto p-0">
                  <table className="min-w-full text-sm">
                    <thead className="bg-slate-50 text-xs uppercase text-slate-500">
                      <tr>
                        <th className="px-4 py-3 text-left font-semibold">Scenario</th>
                        {permissionActions.map((action) => <th key={String(action.key)} className="px-3 py-3 text-center font-semibold">{action.label}</th>)}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-line">
                      {permissions.map((permission) => (
                        <tr key={permission.id}>
                          <td className="min-w-72 px-4 py-3 text-slate-700">{permission.scenario}</td>
                          {permissionActions.map((action) => (
                            <td key={String(action.key)} className="px-3 py-3 text-center">
                              <input type="checkbox" checked={Boolean(permission[action.key])} onChange={() => toggle(permission.id, action.key)} />
                            </td>
                          ))}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </CardBody>
              </Card>
            ))}
          </div>
        </div>
      ) : null}
    </div>
  );
}
