using EProcurement.SharedKernel.Entities;

namespace EProcurement.TenantService.Models;

public abstract class MasterDataEntity : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Department : MasterDataEntity { }
public sealed class CostCenter : MasterDataEntity { }
public sealed class Category : MasterDataEntity { }
public sealed class ProcurementItem : MasterDataEntity { }
public sealed class UnitOfMeasure : MasterDataEntity { }
public sealed class ApprovalWorkflow : MasterDataEntity { }
public sealed class TenderMethodMaster : MasterDataEntity { }
public sealed class EvaluationCriterion : MasterDataEntity { }
public sealed class CommitteeMember : MasterDataEntity { }
public sealed class Currency : MasterDataEntity { }
public sealed class TaxCode : MasterDataEntity { }
public sealed class PaymentTerm : MasterDataEntity { }
public sealed class DeliveryLocation : MasterDataEntity { }
public sealed class DocumentType : MasterDataEntity { }
