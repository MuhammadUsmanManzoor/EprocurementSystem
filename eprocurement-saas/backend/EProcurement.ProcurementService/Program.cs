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
    var query = db.PurchaseRequests.Include(pr => pr.Items).AsQueryable();
    if (!user.IsSuperAdmin)
    {
        query = query.Where(pr => pr.TenantId == user.RequireTenantId());
    }

    return Results.Ok(await query.OrderByDescending(pr => pr.CreatedAtUtc).ToListAsync());
});

group.MapGet("/{id:guid}", async (Guid id, ProcurementDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var pr = await db.PurchaseRequests.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id);
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
    var pr = await db.PurchaseRequests.FindAsync(id);
    if (pr is null) return Results.NotFound();
    if (!user.CanAccessTenant(pr.TenantId)) return Results.Forbid();
    if (pr.Status != PurchaseRequestStatus.Draft) return Results.BadRequest("Only draft purchase requests can be submitted.");

    pr.Status = PurchaseRequestStatus.Submitted;
    pr.SubmittedAtUtc = DateTime.UtcNow;
    pr.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, pr.TenantId, "PurchaseRequestSubmitted", "PurchaseRequest", pr.Id, user.Email, pr.Title);
    return Results.Ok(pr);
});

group.MapPost("/{id:guid}/approve", async (Guid id, ApprovePurchaseRequest request, ProcurementDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsSuperAdmin && user.Role != "Approver" && !user.IsTenantAdmin) return Results.Forbid();

    var pr = await db.PurchaseRequests.FindAsync(id);
    if (pr is null) return Results.NotFound();
    if (!user.CanAccessTenant(pr.TenantId)) return Results.Forbid();
    if (pr.Status != PurchaseRequestStatus.Submitted) return Results.BadRequest("Only submitted purchase requests can be approved or rejected.");

    pr.Status = request.Approved ? PurchaseRequestStatus.Approved : PurchaseRequestStatus.Rejected;
    pr.ApprovalRemarks = request.Remarks.Trim();
    pr.ApprovedByEmail = user.Email;
    pr.ApprovedAtUtc = DateTime.UtcNow;
    pr.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, pr.TenantId, request.Approved ? "PurchaseRequestApproved" : "PurchaseRequestRejected", "PurchaseRequest", pr.Id, user.Email, request.Remarks);
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

static async Task EnsureSchemaAsync(ProcurementDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "PurchaseRequests" ADD COLUMN IF NOT EXISTS "DocEntry" integer NOT NULL DEFAULT 0;
        ALTER TABLE "PurchaseRequests" ADD COLUMN IF NOT EXISTS "DocNum" character varying(40) NOT NULL DEFAULT '';
        ALTER TABLE "PurchaseRequests" ADD COLUMN IF NOT EXISTS "CostCenter" character varying(120) NULL;
        ALTER TABLE "PurchaseRequests" ADD COLUMN IF NOT EXISTS "Category" character varying(120) NULL;
        ALTER TABLE "PurchaseRequests" ADD COLUMN IF NOT EXISTS "Currency" character varying(16) NOT NULL DEFAULT 'USD';
        ALTER TABLE "PurchaseRequestItems" ADD COLUMN IF NOT EXISTS "ItemCode" character varying(120) NULL;
        ALTER TABLE "PurchaseRequestItems" ADD COLUMN IF NOT EXISTS "UnitOfMeasure" character varying(40) NULL;
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
    public List<PurchaseRequestItem> Items { get; set; } = new();
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
public sealed record CreatePurchaseRequest(Guid TenantId, string Title, string Department, string? CostCenter, string? Category, string? Currency, string Justification, List<CreatePurchaseRequestItem> Items);
public sealed record CreatePurchaseRequestItem(string? ItemCode, string Description, string? UnitOfMeasure, decimal Quantity, decimal EstimatedUnitPrice);
public sealed record ApprovePurchaseRequest(bool Approved, string Remarks);

public sealed class ProcurementDbContext : DbContext
{
    public ProcurementDbContext(DbContextOptions<ProcurementDbContext> options) : base(options) { }
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<PurchaseRequestItem> PurchaseRequestItems => Set<PurchaseRequestItem>();
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
            entity.Property(pr => pr.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasMany(pr => pr.Items).WithOne().HasForeignKey(item => item.PurchaseRequestId);
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
    }
}
