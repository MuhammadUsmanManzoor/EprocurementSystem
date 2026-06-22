using EProcurement.SharedKernel.Entities;

namespace EProcurement.AuthService.Models;

public sealed class RolePermission : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid RoleId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanApprove { get; set; }
    public bool CanOpen { get; set; }
    public bool CanEvaluate { get; set; }
    public bool CanAward { get; set; }
    public bool CanGenerate { get; set; }
    public bool CanExport { get; set; }
    public bool CanAudit { get; set; }
}
