"use client";

import { useEffect, useMemo, useState } from "react";
import { KeyRound, Plus, RefreshCw, ShieldCheck, Users } from "lucide-react";
import { api, demoTenantId, RoleAdmin, RolePermission, UserAdmin } from "@/lib/api";
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

type Tab = "master-data" | "users" | "roles";

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
        <TabButton active={tab === "roles"} onClick={() => setTab("roles")}>Roles & Permissions</TabButton>
      </div>
      {tab === "master-data" ? <MasterDataPage /> : null}
      {tab === "users" ? <UsersPanel /> : null}
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
