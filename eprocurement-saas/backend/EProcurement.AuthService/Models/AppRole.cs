using EProcurement.SharedKernel.Entities;

namespace EProcurement.AuthService.Models;

public sealed class AppRole : BaseEntity
{
    public Guid? TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public List<RolePermission> Permissions { get; set; } = new();
}
