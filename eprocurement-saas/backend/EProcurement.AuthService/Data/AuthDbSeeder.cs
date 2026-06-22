using EProcurement.AuthService.Models;
using EProcurement.AuthService.Security;
using Microsoft.EntityFrameworkCore;

namespace EProcurement.AuthService.Data;

public static class AuthDbSeeder
{
    public static readonly Guid AkpkTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task SeedAsync(AuthDbContext db, PasswordHasher hasher)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsureSchemaAsync(db);

        await SeedRolesAsync(db);

        var passwordHash = hasher.Hash("Password123!");
        var users = new[]
        {
            new AppUser { TenantId = null, Email = "superadmin@demo.com", FullName = "Super Admin", Role = "SuperAdmin", PasswordHash = passwordHash },
            new AppUser { TenantId = AkpkTenantId, Email = "tenantadmin@akpk.com", FullName = "AKPK Tenant Admin", Role = "TenantAdmin", PasswordHash = passwordHash },
            new AppUser { TenantId = AkpkTenantId, Email = "procurement@akpk.com", FullName = "Procurement Officer", Role = "Procurement", PasswordHash = passwordHash },
            new AppUser { TenantId = AkpkTenantId, Email = "approver@akpk.com", FullName = "Approval Manager", Role = "Approver", PasswordHash = passwordHash },
            new AppUser { TenantId = AkpkTenantId, Email = "committee@akpk.com", FullName = "Evaluation Committee", Role = "Committee", PasswordHash = passwordHash },
            new AppUser { TenantId = AkpkTenantId, Email = "finance@akpk.com", FullName = "Finance Officer", Role = "Finance", PasswordHash = passwordHash },
            new AppUser { TenantId = AkpkTenantId, Email = "vendor@demo.com", FullName = "Demo Vendor", Role = "Vendor", PasswordHash = passwordHash },
            new AppUser { TenantId = AkpkTenantId, Email = "auditor@akpk.com", FullName = "Audit Reviewer", Role = "Auditor", PasswordHash = passwordHash }
        };

        foreach (var user in users)
        {
            if (await db.Users.AnyAsync(item => item.Email == user.Email))
            {
                continue;
            }

            db.Users.Add(user);
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureSchemaAsync(AuthDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS auth."Roles" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NULL,
                "Code" character varying(64) NOT NULL,
                "Name" character varying(160) NOT NULL,
                "Description" character varying(500) NOT NULL,
                "IsSystem" boolean NOT NULL,
                "IsActive" boolean NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL,
                CONSTRAINT "PK_Roles" PRIMARY KEY ("Id")
            );

            CREATE TABLE IF NOT EXISTS auth."RolePermissions" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NULL,
                "RoleId" uuid NOT NULL,
                "Module" character varying(120) NOT NULL,
                "Scenario" character varying(180) NOT NULL,
                "CanView" boolean NOT NULL,
                "CanCreate" boolean NOT NULL,
                "CanEdit" boolean NOT NULL,
                "CanDelete" boolean NOT NULL,
                "CanSubmit" boolean NOT NULL,
                "CanApprove" boolean NOT NULL,
                "CanOpen" boolean NOT NULL,
                "CanEvaluate" boolean NOT NULL,
                "CanAward" boolean NOT NULL,
                "CanGenerate" boolean NOT NULL,
                "CanExport" boolean NOT NULL,
                "CanAudit" boolean NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL,
                CONSTRAINT "PK_RolePermissions" PRIMARY KEY ("Id")
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Roles_TenantId_Code" ON auth."Roles" ("TenantId", "Code");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RolePermissions_RoleId_Module_Scenario" ON auth."RolePermissions" ("RoleId", "Module", "Scenario");
            CREATE INDEX IF NOT EXISTS "IX_RolePermissions_RoleId" ON auth."RolePermissions" ("RoleId");
            """);
    }

    private static async Task SeedRolesAsync(AuthDbContext db)
    {
        foreach (var template in RoleTemplates)
        {
            var role = await db.Roles
                .Include(item => item.Permissions)
                .SingleOrDefaultAsync(item => item.TenantId == template.TenantId && item.Code == template.Code);

            if (role is null)
            {
                role = new AppRole
                {
                    TenantId = template.TenantId,
                    Code = template.Code,
                    Name = template.Name,
                    Description = template.Description,
                    IsSystem = true,
                    IsActive = true
                };
                db.Roles.Add(role);
                await db.SaveChangesAsync();
            }
            else
            {
                role.Name = template.Name;
                role.Description = template.Description;
                role.IsSystem = true;
                role.IsActive = true;
            }

            foreach (var scenario in PermissionScenarios)
            {
                var existing = role.Permissions.FirstOrDefault(permission => permission.Module == scenario.Module && permission.Scenario == scenario.Scenario);
                var access = template.Allowed.Contains(scenario.Key);
                if (existing is null)
                {
                    db.RolePermissions.Add(new RolePermission
                    {
                        TenantId = template.TenantId,
                        RoleId = role.Id,
                        Module = scenario.Module,
                        Scenario = scenario.Scenario,
                        CanView = access && scenario.CanView,
                        CanCreate = access && scenario.CanCreate,
                        CanEdit = access && scenario.CanEdit,
                        CanDelete = access && scenario.CanDelete,
                        CanSubmit = access && scenario.CanSubmit,
                        CanApprove = access && scenario.CanApprove,
                        CanOpen = access && scenario.CanOpen,
                        CanEvaluate = access && scenario.CanEvaluate,
                        CanAward = access && scenario.CanAward,
                        CanGenerate = access && scenario.CanGenerate,
                        CanExport = access && scenario.CanExport,
                        CanAudit = access && scenario.CanAudit
                    });
                    continue;
                }

                existing.CanView = access && scenario.CanView;
                existing.CanCreate = access && scenario.CanCreate;
                existing.CanEdit = access && scenario.CanEdit;
                existing.CanDelete = access && scenario.CanDelete;
                existing.CanSubmit = access && scenario.CanSubmit;
                existing.CanApprove = access && scenario.CanApprove;
                existing.CanOpen = access && scenario.CanOpen;
                existing.CanEvaluate = access && scenario.CanEvaluate;
                existing.CanAward = access && scenario.CanAward;
                existing.CanGenerate = access && scenario.CanGenerate;
                existing.CanExport = access && scenario.CanExport;
                existing.CanAudit = access && scenario.CanAudit;
            }

            await db.SaveChangesAsync();
        }
    }

    private sealed record RoleTemplate(Guid? TenantId, string Code, string Name, string Description, HashSet<string> Allowed);
    private sealed record PermissionScenario(string Key, string Module, string Scenario, bool CanView = true, bool CanCreate = false, bool CanEdit = false, bool CanDelete = false, bool CanSubmit = false, bool CanApprove = false, bool CanOpen = false, bool CanEvaluate = false, bool CanAward = false, bool CanGenerate = false, bool CanExport = false, bool CanAudit = false);

    private static readonly PermissionScenario[] PermissionScenarios =
    {
        new("Dashboard.View", "Dashboard", "View enterprise dashboard"),
        new("MasterData.Manage", "Administration", "Manage master data", CanCreate: true, CanEdit: true, CanDelete: true),
        new("Users.Manage", "Administration", "Manage users", CanCreate: true, CanEdit: true, CanDelete: true),
        new("Roles.Manage", "Administration", "Manage roles and permissions", CanCreate: true, CanEdit: true, CanDelete: true),
        new("PR.Create", "Purchase Requests", "Create purchase request", CanCreate: true, CanEdit: true, CanDelete: true, CanSubmit: true),
        new("PR.Approve", "Purchase Requests", "Approve purchase request stages", CanApprove: true),
        new("PR.View", "Purchase Requests", "View purchase requests"),
        new("Tender.Manage", "Tendering", "Create and publish tenders", CanCreate: true, CanEdit: true, CanSubmit: true),
        new("Tender.ViewPublished", "Tendering", "View published tenders"),
        new("Bid.Submit", "Bidding", "Submit and revise vendor bids", CanCreate: true, CanEdit: true, CanSubmit: true),
        new("Bid.Open", "Bidding", "Open bids after closing date", CanOpen: true),
        new("Bid.Compare", "Bidding", "Compare opened bids", CanEvaluate: true, CanExport: true),
        new("Evaluation.Manage", "Evaluation", "Evaluate bids and record scores", CanEdit: true, CanEvaluate: true),
        new("Award.Manage", "Award", "Create award decision with justification", CanCreate: true, CanAward: true),
        new("Award.Approve", "Award", "Approve award decision", CanApprove: true),
        new("PO.Generate", "Purchase Orders", "Generate purchase order", CanGenerate: true),
        new("PO.View", "Purchase Orders", "View purchase orders"),
        new("Contract.Manage", "Contracts", "Create and maintain contracts", CanCreate: true, CanEdit: true),
        new("Document.Manage", "Documents", "Create and version documents", CanCreate: true, CanEdit: true, CanDelete: true),
        new("Document.View", "Documents", "View documents"),
        new("Audit.View", "Audit", "View audit logs", CanAudit: true, CanExport: true),
        new("Notification.View", "Notifications", "View notifications"),
        new("SapIntegration.View", "Integrations", "View SAP integration status"),
        new("Vendor.Manage", "Vendors", "Register and approve vendors", CanCreate: true, CanEdit: true, CanApprove: true),
        new("Vendor.OwnData", "Vendors", "Maintain own vendor profile", CanEdit: true)
    };

    private static readonly RoleTemplate[] RoleTemplates =
    {
        Role(null, "SuperAdmin", "Super Admin", "Platform administrator with all tenant access.", PermissionScenarios.Select(item => item.Key).ToArray()),
        Role(AkpkTenantId, "TenantAdmin", "Tenant Admin", "Tenant administrator for setup, users, roles, approvals, audit, and all tenant records.", PermissionScenarios.Where(item => item.Key != "Vendor.OwnData").Select(item => item.Key).ToArray()),
        Role(AkpkTenantId, "Procurement", "Procurement Officer", "Creates PRs, manages tenders, documents, awards, purchase orders, and contracts.", "Dashboard.View", "PR.Create", "PR.View", "PR.Approve", "Tender.Manage", "Bid.Compare", "Evaluation.Manage", "Award.Manage", "PO.Generate", "PO.View", "Contract.Manage", "Document.Manage", "Document.View", "Notification.View", "Vendor.Manage"),
        Role(AkpkTenantId, "Approver", "Approver", "Approves assigned purchase request stages and views related PRs.", "Dashboard.View", "PR.View", "PR.Approve", "Document.View", "Notification.View"),
        Role(AkpkTenantId, "Committee", "Evaluation Committee", "Opens, compares, and evaluates bids.", "Dashboard.View", "Tender.ViewPublished", "Bid.Open", "Bid.Compare", "Evaluation.Manage", "Award.Manage", "Document.View", "Notification.View"),
        Role(AkpkTenantId, "EvaluationCommittee", "Evaluation Committee", "Alias role for committee users in evaluation workflows.", "Dashboard.View", "Tender.ViewPublished", "Bid.Open", "Bid.Compare", "Evaluation.Manage", "Award.Manage", "Document.View", "Notification.View"),
        Role(AkpkTenantId, "Finance", "Finance Officer", "Reviews approvals, awards, purchase orders, payments, tax, and bid comparison.", "Dashboard.View", "PR.View", "PR.Approve", "Bid.Compare", "Award.Manage", "Award.Approve", "PO.Generate", "PO.View", "Document.View", "Notification.View"),
        Role(AkpkTenantId, "Vendor", "Vendor", "External vendor user for published tenders, own bids, own documents, and own profile.", "Dashboard.View", "Tender.ViewPublished", "Bid.Submit", "Document.View", "Notification.View", "Vendor.OwnData"),
        Role(AkpkTenantId, "VendorUser", "Vendor User", "External vendor user alias.", "Dashboard.View", "Tender.ViewPublished", "Bid.Submit", "Document.View", "Notification.View", "Vendor.OwnData"),
        Role(AkpkTenantId, "Auditor", "Auditor", "Read-only audit reviewer with export access.", "Dashboard.View", "PR.View", "Tender.ViewPublished", "Bid.Compare", "PO.View", "Document.View", "Audit.View", "Notification.View")
    };

    private static RoleTemplate Role(Guid? tenantId, string code, string name, string description, params string[] allowed)
    {
        return new RoleTemplate(tenantId, code, name, description, allowed.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }
}
