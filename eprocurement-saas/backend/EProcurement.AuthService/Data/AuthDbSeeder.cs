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

        if (await db.Users.AnyAsync())
        {
            return;
        }

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

        db.Users.AddRange(users);
        await db.SaveChangesAsync();
    }
}
