using System.Security.Claims;

namespace EProcurement.SharedKernel.Security;

public sealed class CurrentUser
{
    public CurrentUser(ClaimsPrincipal principal)
    {
        Id = TryReadGuid(ReadClaim(principal, ClaimTypes.NameIdentifier))
            ?? TryReadGuid(ReadClaim(principal, "sub"))
            ?? Guid.Empty;
        TenantId = TryReadGuid(ReadClaim(principal, "tenant_id"));
        Email = ReadClaim(principal, ClaimTypes.Email) ?? ReadClaim(principal, "email") ?? string.Empty;
        Role = ReadClaim(principal, ClaimTypes.Role) ?? string.Empty;
        VendorId = TryReadGuid(ReadClaim(principal, "vendor_id"));
    }

    public Guid Id { get; }
    public Guid? TenantId { get; }
    public Guid? VendorId { get; }
    public string Email { get; }
    public string Role { get; }
    public bool IsSuperAdmin => Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase);
    public bool IsTenantAdmin => Role.Equals("TenantAdmin", StringComparison.OrdinalIgnoreCase);
    public bool IsVendor => Role.Equals("Vendor", StringComparison.OrdinalIgnoreCase) || Role.Equals("VendorUser", StringComparison.OrdinalIgnoreCase);
    public bool IsCommittee => Role.Equals("Committee", StringComparison.OrdinalIgnoreCase) || Role.Equals("EvaluationCommittee", StringComparison.OrdinalIgnoreCase);

    public Guid RequireTenantId()
    {
        if (TenantId is null && !IsSuperAdmin)
        {
            throw new InvalidOperationException("Tenant context is required.");
        }

        return TenantId ?? Guid.Empty;
    }

    public bool CanAccessTenant(Guid tenantId) => IsSuperAdmin || TenantId == tenantId;

    private static Guid? TryReadGuid(string? value)
    {
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static string? ReadClaim(ClaimsPrincipal principal, string type)
    {
        return principal.FindFirst(type)?.Value;
    }
}
