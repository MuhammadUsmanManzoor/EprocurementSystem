using EProcurement.SharedKernel.Entities;

namespace EProcurement.TenantService.Models;

public sealed class MasterDataItem : TenantEntity
{
    public string Type { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
