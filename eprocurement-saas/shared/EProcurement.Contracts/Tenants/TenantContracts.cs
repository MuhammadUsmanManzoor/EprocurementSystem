namespace EProcurement.Contracts.Tenants;

public sealed record TenantDto(Guid Id, string Name, string Slug, bool IsActive);

public sealed record CreateTenantRequest(string Name, string Slug);

public sealed record UpdateTenantRequest(string Name, string Slug, bool IsActive);
