using EProcurement.TenantService.Models;
using Microsoft.EntityFrameworkCore;

namespace EProcurement.TenantService.Data;

public sealed class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<MasterDataItem> MasterDataItems => Set<MasterDataItem>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProcurementItem> ProcurementItems => Set<ProcurementItem>();
    public DbSet<UnitOfMeasure> UnitOfMeasures => Set<UnitOfMeasure>();
    public DbSet<ApprovalWorkflow> ApprovalWorkflows => Set<ApprovalWorkflow>();
    public DbSet<TenderMethodMaster> TenderMethods => Set<TenderMethodMaster>();
    public DbSet<EvaluationCriterion> EvaluationCriteria => Set<EvaluationCriterion>();
    public DbSet<CommitteeMember> CommitteeMembers => Set<CommitteeMember>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<TaxCode> TaxCodes => Set<TaxCode>();
    public DbSet<PaymentTerm> PaymentTerms => Set<PaymentTerm>();
    public DbSet<DeliveryLocation> DeliveryLocations => Set<DeliveryLocation>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(tenant => tenant.Id);
            entity.HasIndex(tenant => tenant.Slug).IsUnique();
            entity.Property(tenant => tenant.Name).HasMaxLength(160).IsRequired();
            entity.Property(tenant => tenant.Slug).HasMaxLength(80).IsRequired();
        });

        modelBuilder.Entity<MasterDataItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.Type, item.Code }).IsUnique();
            entity.Property(item => item.Type).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(180).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
        });

        ConfigureMasterData<Department>(modelBuilder, "Departments");
        ConfigureMasterData<CostCenter>(modelBuilder, "CostCenters");
        ConfigureMasterData<Category>(modelBuilder, "Categories");
        ConfigureMasterData<ProcurementItem>(modelBuilder, "ProcurementItems");
        ConfigureMasterData<UnitOfMeasure>(modelBuilder, "UnitOfMeasures");
        ConfigureMasterData<ApprovalWorkflow>(modelBuilder, "ApprovalWorkflows");
        ConfigureMasterData<TenderMethodMaster>(modelBuilder, "TenderMethods");
        ConfigureMasterData<EvaluationCriterion>(modelBuilder, "EvaluationCriteria");
        ConfigureMasterData<CommitteeMember>(modelBuilder, "CommitteeMembers");
        ConfigureMasterData<Currency>(modelBuilder, "Currencies");
        ConfigureMasterData<TaxCode>(modelBuilder, "TaxCodes");
        ConfigureMasterData<PaymentTerm>(modelBuilder, "PaymentTerms");
        ConfigureMasterData<DeliveryLocation>(modelBuilder, "DeliveryLocations");
        ConfigureMasterData<DocumentType>(modelBuilder, "DocumentTypes");
    }

    private static void ConfigureMasterData<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : MasterDataEntity
    {
        modelBuilder.Entity<TEntity>(entity =>
        {
            entity.ToTable(tableName);
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.Code }).IsUnique();
            entity.Property(item => item.Code).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(180).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
        });
    }
}
