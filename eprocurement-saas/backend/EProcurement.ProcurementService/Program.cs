using System.Text;
using System.Text.Json.Serialization;
using EProcurement.SharedKernel.Audit;
using EProcurement.SharedKernel.Demo;
using EProcurement.SharedKernel.Entities;
using EProcurement.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ProcurementDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient("audit", client => client.BaseAddress = new Uri(builder.Configuration["Services:Audit"] ?? "http://audit-service:8080"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "eprocurement",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "eprocurement-web",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? "development-secret-change-me-development-secret"))
    };
});
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "procurement-service" }));

var group = app.MapGroup("/api/purchase-requests").RequireAuthorization();

group.MapGet("/", async (ProcurementDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var query = db.PurchaseRequests
        .Include(pr => pr.Items)
        .Include(pr => pr.ApprovalStages)
        .ThenInclude(stage => stage.Approvers)
        .AsQueryable();
    if (!user.IsSuperAdmin)
    {
        query = query.Where(pr => pr.TenantId == user.RequireTenantId());
    }

    return Results.Ok(await query.OrderByDescending(pr => pr.CreatedAtUtc).ToListAsync());
});

group.MapGet("/{id:guid}", async (Guid id, ProcurementDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var pr = await db.PurchaseRequests
        .Include(x => x.Items)
        .Include(pr => pr.ApprovalStages)
        .ThenInclude(stage => stage.Approvers)
        .SingleOrDefaultAsync(x => x.Id == id);
    if (pr is null) return Results.NotFound();
    if (!user.CanAccessTenant(pr.TenantId)) return Results.Forbid();
    return Results.Ok(pr);
});

group.MapPost("/", async (CreatePurchaseRequest request, ProcurementDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var tenantId = user.IsSuperAdmin ? request.TenantId : user.RequireTenantId();
    if (tenantId == Guid.Empty)
    {
        return Results.BadRequest("TenantId is required.");
    }

    var nextDocEntry = await GetNextDocEntryAsync(db, tenantId);
    var pr = new PurchaseRequest
    {
        TenantId = tenantId,
        DocEntry = nextDocEntry,
        DocNum = BuildDocNum(nextDocEntry),
        Title = request.Title.Trim(),
        Department = request.Department.Trim(),
        CostCenter = request.CostCenter?.Trim(),
        Category = request.Category?.Trim(),
        Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim().ToUpperInvariant(),
        RequestedByEmail = user.Email,
        Justification = request.Justification.Trim(),
        Status = PurchaseRequestStatus.Draft,
        Items = request.Items.Select(item => new PurchaseRequestItem
        {
            TenantId = tenantId,
            ItemCode = item.ItemCode?.Trim(),
            Description = item.Description.Trim(),
            UnitOfMeasure = item.UnitOfMeasure?.Trim(),
            Quantity = item.Quantity,
            EstimatedUnitPrice = item.EstimatedUnitPrice
        }).ToList()
    };

    db.PurchaseRequests.Add(pr);
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, tenantId, "PurchaseRequestCreated", "PurchaseRequest", pr.Id, user.Email, pr.Title);
    return Results.Created($"/api/purchase-requests/{pr.Id}", pr);
});

group.MapPost("/{id:guid}/submit", async (Guid id, ProcurementDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var pr = await db.PurchaseRequests
        .Include(x => x.ApprovalStages)
        .ThenInclude(stage => stage.Approvers)
        .SingleOrDefaultAsync(x => x.Id == id);
    if (pr is null) return Results.NotFound();
    if (!user.CanAccessTenant(pr.TenantId)) return Results.Forbid();
    if (pr.Status != PurchaseRequestStatus.Draft) return Results.BadRequest("Only draft purchase requests can be submitted.");

    pr.Status = PurchaseRequestStatus.Submitted;
    await EnsureApprovalWorkflowAsync(db, pr);
    SetCurrentApprovalStage(pr);
    pr.SubmittedAtUtc = DateTime.UtcNow;
    pr.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, pr.TenantId, "PurchaseRequestSubmitted", "PurchaseRequest", pr.Id, user.Email, pr.Title);
    return Results.Ok(pr);
});

group.MapPost("/{id:guid}/approve", async (Guid id, ApprovePurchaseRequest request, ProcurementDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);

    var pr = await db.PurchaseRequests
        .Include(x => x.ApprovalStages)
        .ThenInclude(stage => stage.Approvers)
        .SingleOrDefaultAsync(x => x.Id == id);
    if (pr is null) return Results.NotFound();
    if (!user.CanAccessTenant(pr.TenantId)) return Results.Forbid();
    if (pr.Status != PurchaseRequestStatus.Submitted) return Results.BadRequest("Only submitted purchase requests can be approved or rejected.");

    await EnsureApprovalWorkflowAsync(db, pr);
    var currentStage = GetCurrentApprovalStage(pr);
    if (currentStage is null) return Results.BadRequest("No pending approval stage was found.");

    var assignedApprover = currentStage.Approvers
        .FirstOrDefault(approver => approver.ApproverEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
    if (!user.IsSuperAdmin && !user.IsTenantAdmin && assignedApprover is null)
    {
        return Results.Forbid();
    }

    pr.ApprovalRemarks = request.Remarks.Trim();
    pr.UpdatedAtUtc = DateTime.UtcNow;

    if (!request.Approved)
    {
        pr.Status = PurchaseRequestStatus.Rejected;
        currentStage.Status = ApprovalStageStatus.Rejected;
        currentStage.ActionedByEmail = user.Email;
        currentStage.ActionedAtUtc = DateTime.UtcNow;
        currentStage.Remarks = request.Remarks.Trim();
        if (assignedApprover is not null)
        {
            assignedApprover.Status = ApprovalApproverStatus.Rejected;
            assignedApprover.ActionedAtUtc = DateTime.UtcNow;
            assignedApprover.Remarks = request.Remarks.Trim();
        }

        await db.SaveChangesAsync();
        await AuditAsync(httpClientFactory, pr.TenantId, "PurchaseRequestRejected", "PurchaseRequest", pr.Id, user.Email, $"{currentStage.StageName}: {request.Remarks}");
        return Results.Ok(pr);
    }

    currentStage.Status = ApprovalStageStatus.Approved;
    currentStage.ActionedByEmail = user.Email;
    currentStage.ActionedAtUtc = DateTime.UtcNow;
    currentStage.Remarks = request.Remarks.Trim();
    if (assignedApprover is not null)
    {
        assignedApprover.Status = ApprovalApproverStatus.Approved;
        assignedApprover.ActionedAtUtc = DateTime.UtcNow;
        assignedApprover.Remarks = request.Remarks.Trim();
    }

    var nextStage = GetCurrentApprovalStage(pr);
    if (nextStage is null)
    {
        pr.Status = PurchaseRequestStatus.Approved;
        pr.ApprovedByEmail = user.Email;
        pr.ApprovedAtUtc = DateTime.UtcNow;
        pr.CurrentApprovalStageOrder = null;
        pr.CurrentApprovalStageName = null;
    }
    else
    {
        pr.CurrentApprovalStageOrder = nextStage.StageOrder;
        pr.CurrentApprovalStageName = nextStage.StageName;
    }

    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, pr.TenantId, pr.Status == PurchaseRequestStatus.Approved ? "PurchaseRequestApproved" : "PurchaseRequestApprovalStageApproved", "PurchaseRequest", pr.Id, user.Email, $"{currentStage.StageName}: {request.Remarks}");
    return Results.Ok(pr);
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
    await db.Database.EnsureCreatedAsync();
    await EnsureSchemaAsync(db);
    await SeedDemoDataAsync(db);
}

app.Run();

static async Task AuditAsync(IHttpClientFactory factory, Guid tenantId, string action, string entity, Guid entityId, string actor, string details)
{
    try { await factory.CreateClient("audit").PostAsJsonAsync("/api/audit-logs", new AuditEventRequest(tenantId, "ProcurementService", action, entity, entityId, actor, details)); } catch { }
}

static async Task<int> GetNextDocEntryAsync(ProcurementDbContext db, Guid tenantId)
{
    var maxDocEntry = await db.PurchaseRequests
        .Where(pr => pr.TenantId == tenantId)
        .MaxAsync(pr => (int?)pr.DocEntry) ?? 0;
    return maxDocEntry + 1;
}

static string BuildDocNum(int docEntry) => $"PR-{DateTime.UtcNow:yyyy}-{docEntry:000000}";

static Task EnsureApprovalWorkflowAsync(ProcurementDbContext db, PurchaseRequest pr)
{
    if (pr.ApprovalStages.Any())
    {
        return Task.CompletedTask;
    }

    var stages = new[]
    {
        CreateApprovalStage(pr, 1, "Stage 1 - Department Approval", new[] { "approver@akpk.com" }),
        CreateApprovalStage(pr, 2, "Stage 2 - Finance and Procurement Review", new[] { "finance@akpk.com", "procurement@akpk.com", "tenantadmin@akpk.com" }),
        CreateApprovalStage(pr, 3, "Stage 3 - Final Approval", new[] { "tenantadmin@akpk.com", "approver@akpk.com" })
    };

    foreach (var stage in stages)
    {
        pr.ApprovalStages.Add(stage);
        db.Entry(stage).State = EntityState.Added;
        foreach (var approver in stage.Approvers)
        {
            db.Entry(approver).State = EntityState.Added;
        }
    }

    SetCurrentApprovalStage(pr);
    return Task.CompletedTask;
}

static PurchaseRequestApprovalStage CreateApprovalStage(PurchaseRequest pr, int stageOrder, string stageName, IEnumerable<string> approverEmails)
{
    var stage = new PurchaseRequestApprovalStage
    {
        TenantId = pr.TenantId,
        PurchaseRequestId = pr.Id,
        StageOrder = stageOrder,
        StageName = stageName,
        ApprovalMode = ApprovalStageMode.AnyOne,
        Status = ApprovalStageStatus.Pending
    };

    stage.Approvers = approverEmails.Select(email => new PurchaseRequestApprovalStageApprover
    {
        TenantId = pr.TenantId,
        ApprovalStageId = stage.Id,
        ApproverEmail = email,
        Status = ApprovalApproverStatus.Pending
    }).ToList();

    return stage;
}

static PurchaseRequestApprovalStage? GetCurrentApprovalStage(PurchaseRequest pr)
{
    return pr.ApprovalStages
        .Where(stage => stage.Status == ApprovalStageStatus.Pending)
        .OrderBy(stage => stage.StageOrder)
        .FirstOrDefault();
}

static void SetCurrentApprovalStage(PurchaseRequest pr)
{
    var stage = GetCurrentApprovalStage(pr);
    pr.CurrentApprovalStageOrder = stage?.StageOrder;
    pr.CurrentApprovalStageName = stage?.StageName;
}

static async Task EnsureSchemaAsync(ProcurementDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "PurchaseRequests" ADD COLUMN IF NOT EXISTS "DocEntry" integer NOT NULL DEFAULT 0;
        ALTER TABLE "PurchaseRequests" ADD COLUMN IF NOT EXISTS "DocNum" character varying(40) NOT NULL DEFAULT '';
        ALTER TABLE "PurchaseRequests" ADD COLUMN IF NOT EXISTS "CostCenter" character varying(120) NULL;
        ALTER TABLE "PurchaseRequests" ADD COLUMN IF NOT EXISTS "Category" character varying(120) NULL;
        ALTER TABLE "PurchaseRequests" ADD COLUMN IF NOT EXISTS "Currency" character varying(16) NOT NULL DEFAULT 'USD';
        ALTER TABLE "PurchaseRequests" ADD COLUMN IF NOT EXISTS "CurrentApprovalStageOrder" integer NULL;
        ALTER TABLE "PurchaseRequests" ADD COLUMN IF NOT EXISTS "CurrentApprovalStageName" character varying(160) NULL;
        ALTER TABLE "PurchaseRequestItems" ADD COLUMN IF NOT EXISTS "ItemCode" character varying(120) NULL;
        ALTER TABLE "PurchaseRequestItems" ADD COLUMN IF NOT EXISTS "UnitOfMeasure" character varying(40) NULL;
        """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "PurchaseRequestApprovalStages" (
            "Id" uuid NOT NULL,
            "TenantId" uuid NOT NULL,
            "PurchaseRequestId" uuid NOT NULL,
            "StageOrder" integer NOT NULL,
            "StageName" character varying(160) NOT NULL,
            "ApprovalMode" character varying(32) NOT NULL,
            "Status" character varying(32) NOT NULL,
            "ActionedByEmail" character varying(180) NULL,
            "ActionedAtUtc" timestamp with time zone NULL,
            "Remarks" character varying(500) NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "UpdatedAtUtc" timestamp with time zone NULL,
            CONSTRAINT "PK_PurchaseRequestApprovalStages" PRIMARY KEY ("Id")
        );

        CREATE TABLE IF NOT EXISTS "PurchaseRequestApprovalStageApprovers" (
            "Id" uuid NOT NULL,
            "TenantId" uuid NOT NULL,
            "ApprovalStageId" uuid NOT NULL,
            "ApproverEmail" character varying(180) NOT NULL,
            "Status" character varying(32) NOT NULL,
            "ActionedAtUtc" timestamp with time zone NULL,
            "Remarks" character varying(500) NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "UpdatedAtUtc" timestamp with time zone NULL,
            CONSTRAINT "PK_PurchaseRequestApprovalStageApprovers" PRIMARY KEY ("Id")
        );

        CREATE INDEX IF NOT EXISTS "IX_PurchaseRequestApprovalStages_PurchaseRequestId_StageOrder"
            ON "PurchaseRequestApprovalStages" ("PurchaseRequestId", "StageOrder");
        CREATE INDEX IF NOT EXISTS "IX_PurchaseRequestApprovalStageApprovers_ApprovalStageId"
            ON "PurchaseRequestApprovalStageApprovers" ("ApprovalStageId");
        """);

    await db.Database.ExecuteSqlRawAsync("""
        WITH numbered AS (
            SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "TenantId" ORDER BY "CreatedAtUtc", "Id")::integer AS rn
            FROM "PurchaseRequests"
            WHERE "DocEntry" = 0 OR "DocNum" = ''
        )
        UPDATE "PurchaseRequests" pr
        SET "DocEntry" = numbered.rn,
            "DocNum" = 'PR-' || EXTRACT(YEAR FROM pr."CreatedAtUtc")::integer || '-' || LPAD(numbered.rn::text, 6, '0')
        FROM numbered
        WHERE pr."Id" = numbered."Id";
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PurchaseRequests_TenantId_DocEntry" ON "PurchaseRequests" ("TenantId", "DocEntry");
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_PurchaseRequests_TenantId_DocNum" ON "PurchaseRequests" ("TenantId", "DocNum");
        """);
}

static async Task SeedDemoDataAsync(ProcurementDbContext db)
{
    if (await db.PurchaseRequests.AnyAsync(pr => pr.Id == DemoDataIds.ApprovedPurchaseRequestId))
    {
        return;
    }

    db.PurchaseRequests.Add(new PurchaseRequest
    {
        Id = DemoDataIds.ApprovedPurchaseRequestId,
        TenantId = DemoDataIds.TenantId,
        DocEntry = 1,
        DocNum = "PR-2026-000001",
        Title = "Laptop Refresh for Procurement Team",
        Department = "Procurement",
        CostCenter = "CC-PROC-001",
        Category = "CAT-IT",
        Currency = "USD",
        RequestedByEmail = "procurement@akpk.com",
        Justification = "Replace aging laptops used for tender preparation, bid evaluation, and contract administration.",
        Status = PurchaseRequestStatus.Approved,
        SubmittedAtUtc = DateTime.UtcNow.AddDays(-12),
        ApprovedAtUtc = DateTime.UtcNow.AddDays(-10),
        ApprovedByEmail = "approver@akpk.com",
        ApprovalRemarks = "Approved for tender creation.",
        Items = new List<PurchaseRequestItem>
        {
            new()
            {
                TenantId = DemoDataIds.TenantId,
                ItemCode = "IT-LAPTOP",
                Description = "Business laptop with docking station",
                UnitOfMeasure = "EA",
                Quantity = 10,
                EstimatedUnitPrice = 5200
            }
        }
    });

    await db.SaveChangesAsync();
}

public sealed class PurchaseRequest : TenantEntity
{
    public int DocEntry { get; set; }
    public string DocNum { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? CostCenter { get; set; }
    public string? Category { get; set; }
    public string Currency { get; set; } = "USD";
    public string RequestedByEmail { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Draft;
    public string? ApprovedByEmail { get; set; }
    public string? ApprovalRemarks { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public int? CurrentApprovalStageOrder { get; set; }
    public string? CurrentApprovalStageName { get; set; }
    public List<PurchaseRequestItem> Items { get; set; } = new();
    public List<PurchaseRequestApprovalStage> ApprovalStages { get; set; } = new();
}

public sealed class PurchaseRequestItem : TenantEntity
{
    public Guid PurchaseRequestId { get; set; }
    public string? ItemCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? UnitOfMeasure { get; set; }
    public decimal Quantity { get; set; }
    public decimal EstimatedUnitPrice { get; set; }
}

public enum PurchaseRequestStatus { Draft, Submitted, Approved, Rejected, ConvertedToTender }

public sealed class PurchaseRequestApprovalStage : TenantEntity
{
    public Guid PurchaseRequestId { get; set; }
    public int StageOrder { get; set; }
    public string StageName { get; set; } = string.Empty;
    public ApprovalStageMode ApprovalMode { get; set; } = ApprovalStageMode.AnyOne;
    public ApprovalStageStatus Status { get; set; } = ApprovalStageStatus.Pending;
    public string? ActionedByEmail { get; set; }
    public DateTime? ActionedAtUtc { get; set; }
    public string? Remarks { get; set; }
    public List<PurchaseRequestApprovalStageApprover> Approvers { get; set; } = new();
}

public sealed class PurchaseRequestApprovalStageApprover : TenantEntity
{
    public Guid ApprovalStageId { get; set; }
    public string ApproverEmail { get; set; } = string.Empty;
    public ApprovalApproverStatus Status { get; set; } = ApprovalApproverStatus.Pending;
    public DateTime? ActionedAtUtc { get; set; }
    public string? Remarks { get; set; }
}

public enum ApprovalStageMode { AnyOne }
public enum ApprovalStageStatus { Pending, Approved, Rejected }
public enum ApprovalApproverStatus { Pending, Approved, Rejected }

public sealed record CreatePurchaseRequest(Guid TenantId, string Title, string Department, string? CostCenter, string? Category, string? Currency, string Justification, List<CreatePurchaseRequestItem> Items);
public sealed record CreatePurchaseRequestItem(string? ItemCode, string Description, string? UnitOfMeasure, decimal Quantity, decimal EstimatedUnitPrice);
public sealed record ApprovePurchaseRequest(bool Approved, string Remarks);

public sealed class ProcurementDbContext : DbContext
{
    public ProcurementDbContext(DbContextOptions<ProcurementDbContext> options) : base(options) { }
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<PurchaseRequestItem> PurchaseRequestItems => Set<PurchaseRequestItem>();
    public DbSet<PurchaseRequestApprovalStage> PurchaseRequestApprovalStages => Set<PurchaseRequestApprovalStage>();
    public DbSet<PurchaseRequestApprovalStageApprover> PurchaseRequestApprovalStageApprovers => Set<PurchaseRequestApprovalStageApprover>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchaseRequest>(entity =>
        {
            entity.HasKey(pr => pr.Id);
            entity.HasIndex(pr => new { pr.TenantId, pr.DocEntry }).IsUnique();
            entity.HasIndex(pr => new { pr.TenantId, pr.DocNum }).IsUnique();
            entity.Property(pr => pr.DocNum).HasMaxLength(40).IsRequired();
            entity.Property(pr => pr.Title).HasMaxLength(200).IsRequired();
            entity.Property(pr => pr.Department).HasMaxLength(120).IsRequired();
            entity.Property(pr => pr.CostCenter).HasMaxLength(120);
            entity.Property(pr => pr.Category).HasMaxLength(120);
            entity.Property(pr => pr.Currency).HasMaxLength(16).IsRequired();
            entity.Property(pr => pr.CurrentApprovalStageName).HasMaxLength(160);
            entity.Property(pr => pr.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasMany(pr => pr.Items).WithOne().HasForeignKey(item => item.PurchaseRequestId);
            entity.HasMany(pr => pr.ApprovalStages).WithOne().HasForeignKey(stage => stage.PurchaseRequestId);
        });
        modelBuilder.Entity<PurchaseRequestItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ItemCode).HasMaxLength(120);
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.UnitOfMeasure).HasMaxLength(40);
            entity.Property(item => item.Quantity).HasPrecision(18, 2);
            entity.Property(item => item.EstimatedUnitPrice).HasPrecision(18, 2);
        });
        modelBuilder.Entity<PurchaseRequestApprovalStage>(entity =>
        {
            entity.HasKey(stage => stage.Id);
            entity.HasIndex(stage => new { stage.PurchaseRequestId, stage.StageOrder });
            entity.Property(stage => stage.StageName).HasMaxLength(160).IsRequired();
            entity.Property(stage => stage.ApprovalMode).HasConversion<string>().HasMaxLength(32);
            entity.Property(stage => stage.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(stage => stage.ActionedByEmail).HasMaxLength(180);
            entity.Property(stage => stage.Remarks).HasMaxLength(500);
            entity.HasMany(stage => stage.Approvers).WithOne().HasForeignKey(approver => approver.ApprovalStageId);
        });
        modelBuilder.Entity<PurchaseRequestApprovalStageApprover>(entity =>
        {
            entity.HasKey(approver => approver.Id);
            entity.HasIndex(approver => approver.ApprovalStageId);
            entity.Property(approver => approver.ApproverEmail).HasMaxLength(180).IsRequired();
            entity.Property(approver => approver.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(approver => approver.Remarks).HasMaxLength(500);
        });
    }
}
