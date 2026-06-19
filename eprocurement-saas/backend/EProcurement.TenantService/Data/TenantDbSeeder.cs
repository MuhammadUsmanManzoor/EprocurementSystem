using EProcurement.TenantService.Models;
using Microsoft.EntityFrameworkCore;

namespace EProcurement.TenantService.Data;

public static class TenantDbSeeder
{
    public static readonly Guid AkpkTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task SeedAsync(TenantDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsureMasterDataTableAsync(db);

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

        await SeedMasterDataAsync(db);
    }

    private static async Task EnsureMasterDataTableAsync(TenantDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "MasterDataItems" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NOT NULL,
                "Type" character varying(80) NOT NULL,
                "Code" character varying(80) NOT NULL,
                "Name" character varying(180) NOT NULL,
                "Description" character varying(500) NULL,
                "IsActive" boolean NOT NULL,
                "CreatedAtUtc" timestamp with time zone NOT NULL,
                "UpdatedAtUtc" timestamp with time zone NULL,
                CONSTRAINT "PK_MasterDataItems" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MasterDataItems_TenantId_Type_Code"
                ON "MasterDataItems" ("TenantId", "Type", "Code");
            """);
    }

    private static async Task SeedMasterDataAsync(TenantDbContext db)
    {
        if (await db.MasterDataItems.AnyAsync(item => item.TenantId == AkpkTenantId))
        {
            return;
        }

        db.MasterDataItems.AddRange(
            Item("Department", "PROC", "Procurement", "Buying and sourcing operations."),
            Item("Department", "FIN", "Finance", "Budget, payment, and purchase order control."),
            Item("Department", "IT", "Information Technology", "Technology assets and services."),
            Item("CostCenter", "CC-PROC-001", "Procurement Operations", "Default procurement operating budget."),
            Item("CostCenter", "CC-IT-001", "IT Shared Services", "Technology procurement budget."),
            Item("Category", "CAT-IT", "IT Equipment", "Laptops, network equipment, and accessories."),
            Item("Category", "CAT-SVC", "Professional Services", "Consulting and implementation services."),
            Item("Item", "IT-LAPTOP", "Business Laptop Bundle", "Laptop with docking station and warranty."),
            Item("Item", "NET-SWITCH", "Network Switch", "Managed switch for office network refresh."),
            Item("UnitOfMeasure", "EA", "Each", "Single unit."),
            Item("UnitOfMeasure", "LOT", "Lot", "Grouped procurement lot."),
            Item("ApprovalWorkflow", "PR-STD", "Standard PR Approval", "Requester submits, approver approves, procurement converts to tender."),
            Item("TenderMethod", "RFQ", "Request for Quotation", "Price-focused sourcing event."),
            Item("TenderMethod", "RFP", "Request for Proposal", "Technical and commercial proposal event."),
            Item("EvaluationCriteria", "TECH", "Technical Compliance", "Technical score and compliance review."),
            Item("EvaluationCriteria", "PRICE", "Commercial Price", "Financial comparison and lowest bid highlight."),
            Item("CommitteeMember", "COM-001", "Evaluation Committee", "Default seeded committee user."),
            Item("Currency", "USD", "US Dollar", "Default demo currency."),
            Item("Currency", "MYR", "Malaysian Ringgit", "Local procurement currency option."),
            Item("TaxCode", "TAX-STD", "Standard Tax", "Default taxable procurement code."),
            Item("PaymentTerm", "NET30", "Net 30 Days", "Payment due within 30 days after invoice."),
            Item("DeliveryLocation", "AKPK-HQ", "AKPK Headquarters", "Default delivery location."),
            Item("DocumentType", "RFQ-DOC", "RFQ Document", "Tender or RFQ attachment."),
            Item("DocumentType", "BID-DOC", "Bid Document", "Vendor bid attachment."),
            Item("DocumentType", "CONTRACT", "Contract Document", "Signed contract or addendum."));

        await db.SaveChangesAsync();
    }

    private static MasterDataItem Item(string type, string code, string name, string description)
    {
        return new MasterDataItem
        {
            Id = Guid.NewGuid(),
            TenantId = AkpkTenantId,
            Type = type,
            Code = code,
            Name = name,
            Description = description,
            IsActive = true
        };
    }
}
