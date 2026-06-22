using EProcurement.TenantService.Models;
using Microsoft.EntityFrameworkCore;

namespace EProcurement.TenantService.Data;

public static class TenantDbSeeder
{
    public static readonly Guid AkpkTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task SeedAsync(TenantDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsureMasterDataTablesAsync(db);

        if (!await db.Tenants.AnyAsync())
        {
            db.Tenants.Add(new Tenant
            {
                Id = AkpkTenantId,
                Name = "AKPK Demo",
                Slug = "akpk-demo",
                IsActive = true
            });

            await db.SaveChangesAsync();
        }

        await MigrateLegacyMasterDataAsync(db);
        await SeedMasterDataAsync(db);
    }

    private static async Task EnsureMasterDataTablesAsync(TenantDbContext db)
    {
        foreach (var table in MasterTableNames)
        {
            await db.Database.ExecuteSqlRawAsync($"""
                CREATE TABLE IF NOT EXISTS "{table}" (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NOT NULL,
                    "Code" character varying(80) NOT NULL,
                    "Name" character varying(180) NOT NULL,
                    "Description" character varying(500) NULL,
                    "IsActive" boolean NOT NULL,
                    "CreatedAtUtc" timestamp with time zone NOT NULL,
                    "UpdatedAtUtc" timestamp with time zone NULL,
                    CONSTRAINT "PK_{table}" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_{table}_TenantId_Code"
                    ON "{table}" ("TenantId", "Code");
                """);
        }
    }

    private static async Task MigrateLegacyMasterDataAsync(TenantDbContext db)
    {
        if (!await db.MasterDataItems.AnyAsync())
        {
            return;
        }

        await CopyLegacyAsync<Department>(db, "Department");
        await CopyLegacyAsync<CostCenter>(db, "CostCenter");
        await CopyLegacyAsync<Category>(db, "Category");
        await CopyLegacyAsync<ProcurementItem>(db, "Item");
        await CopyLegacyAsync<UnitOfMeasure>(db, "UnitOfMeasure");
        await CopyLegacyAsync<ApprovalWorkflow>(db, "ApprovalWorkflow");
        await CopyLegacyAsync<TenderMethodMaster>(db, "TenderMethod");
        await CopyLegacyAsync<EvaluationCriterion>(db, "EvaluationCriteria");
        await CopyLegacyAsync<CommitteeMember>(db, "CommitteeMember");
        await CopyLegacyAsync<Currency>(db, "Currency");
        await CopyLegacyAsync<TaxCode>(db, "TaxCode");
        await CopyLegacyAsync<PaymentTerm>(db, "PaymentTerm");
        await CopyLegacyAsync<DeliveryLocation>(db, "DeliveryLocation");
        await CopyLegacyAsync<DocumentType>(db, "DocumentType");
    }

    private static async Task CopyLegacyAsync<TEntity>(TenantDbContext db, string type)
        where TEntity : MasterDataEntity, new()
    {
        var target = db.Set<TEntity>();
        var legacyItems = await db.MasterDataItems
            .Where(item => item.Type == type)
            .ToListAsync();

        foreach (var legacy in legacyItems)
        {
            var exists = await target.AnyAsync(item => item.TenantId == legacy.TenantId && item.Code == legacy.Code);
            if (exists)
            {
                continue;
            }

            target.Add(new TEntity
            {
                Id = legacy.Id,
                TenantId = legacy.TenantId,
                Code = legacy.Code,
                Name = legacy.Name,
                Description = legacy.Description,
                IsActive = legacy.IsActive,
                CreatedAtUtc = legacy.CreatedAtUtc,
                UpdatedAtUtc = legacy.UpdatedAtUtc
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedMasterDataAsync(TenantDbContext db)
    {
        await SeedAsync(db.Departments, "PROC", "Procurement", "Buying and sourcing operations.");
        await SeedAsync(db.Departments, "FIN", "Finance", "Budget, payment, and purchase order control.");
        await SeedAsync(db.Departments, "IT", "Information Technology", "Technology assets and services.");
        await SeedAsync(db.CostCenters, "CC-PROC-001", "Procurement Operations", "Default procurement operating budget.");
        await SeedAsync(db.CostCenters, "CC-IT-001", "IT Shared Services", "Technology procurement budget.");
        await SeedAsync(db.Categories, "CAT-IT", "IT Equipment", "Laptops, network equipment, and accessories.");
        await SeedAsync(db.Categories, "CAT-SVC", "Professional Services", "Consulting and implementation services.");
        await SeedAsync(db.ProcurementItems, "IT-LAPTOP", "Business Laptop Bundle", "Laptop with docking station and warranty.");
        await SeedAsync(db.ProcurementItems, "NET-SWITCH", "Network Switch", "Managed switch for office network refresh.");
        await SeedAsync(db.UnitOfMeasures, "EA", "Each", "Single unit.");
        await SeedAsync(db.UnitOfMeasures, "LOT", "Lot", "Grouped procurement lot.");
        await SeedAsync(db.ApprovalWorkflows, "PR-STD", "Standard PR Approval", "Requester submits, approver approves, procurement converts to tender.");
        await SeedAsync(db.TenderMethods, "RFQ", "Request for Quotation", "Price-focused sourcing event.");
        await SeedAsync(db.TenderMethods, "RFP", "Request for Proposal", "Technical and commercial proposal event.");
        await SeedAsync(db.EvaluationCriteria, "TECH", "Technical Compliance", "Technical score and compliance review.");
        await SeedAsync(db.EvaluationCriteria, "PRICE", "Commercial Price", "Financial comparison and lowest bid highlight.");
        await SeedAsync(db.CommitteeMembers, "COM-001", "Evaluation Committee", "Default seeded committee user.");
        await SeedAsync(db.Currencies, "USD", "US Dollar", "Default demo currency.");
        await SeedAsync(db.Currencies, "MYR", "Malaysian Ringgit", "Local procurement currency option.");
        await SeedAsync(db.TaxCodes, "TAX-STD", "Standard Tax", "Default taxable procurement code.");
        await SeedAsync(db.PaymentTerms, "NET30", "Net 30 Days", "Payment due within 30 days after invoice.");
        await SeedAsync(db.DeliveryLocations, "AKPK-HQ", "AKPK Headquarters", "Default delivery location.");
        await SeedAsync(db.DocumentTypes, "RFQ-DOC", "RFQ Document", "Tender or RFQ attachment.");
        await SeedAsync(db.DocumentTypes, "BID-DOC", "Bid Document", "Vendor bid attachment.");
        await SeedAsync(db.DocumentTypes, "CONTRACT", "Contract Document", "Signed contract or addendum.");

        await db.SaveChangesAsync();
    }

    private static async Task SeedAsync<TEntity>(DbSet<TEntity> set, string code, string name, string description)
        where TEntity : MasterDataEntity, new()
    {
        if (await set.AnyAsync(item => item.TenantId == AkpkTenantId && item.Code == code))
        {
            return;
        }

        set.Add(new TEntity
        {
            Id = Guid.NewGuid(),
            TenantId = AkpkTenantId,
            Code = code,
            Name = name,
            Description = description,
            IsActive = true
        });
    }

    private static readonly string[] MasterTableNames =
    {
        "Departments",
        "CostCenters",
        "Categories",
        "ProcurementItems",
        "UnitOfMeasures",
        "ApprovalWorkflows",
        "TenderMethods",
        "EvaluationCriteria",
        "CommitteeMembers",
        "Currencies",
        "TaxCodes",
        "PaymentTerms",
        "DeliveryLocations",
        "DocumentTypes"
    };
}
