using EProcurement.SharedKernel.Entities;

namespace EProcurement.TenantService.Models;

public sealed class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
