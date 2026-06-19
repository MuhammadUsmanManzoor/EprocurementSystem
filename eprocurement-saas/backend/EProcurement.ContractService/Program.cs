using System.Text;
using System.Text.Json.Serialization;
using EProcurement.SharedKernel.Audit;
using EProcurement.SharedKernel.Entities;
using EProcurement.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ContractDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
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
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "contract-service" }));

var awards = app.MapGroup("/api/awards").RequireAuthorization();
awards.MapGet("/", async (ContractDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var query = db.AwardDecisions.AsQueryable();
    if (!user.IsSuperAdmin) query = query.Where(a => a.TenantId == user.RequireTenantId());
    if (user.IsVendor && user.VendorId is not null) query = query.Where(a => a.VendorId == user.VendorId);
    return Results.Ok(await query.OrderByDescending(a => a.CreatedAtUtc).ToListAsync());
});

awards.MapGet("/{id:guid}", async (Guid id, ContractDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var award = await db.AwardDecisions.FindAsync(id);
    if (award is null) return Results.NotFound();
    if (!user.CanAccessTenant(award.TenantId)) return Results.Forbid();
    if (user.IsVendor && award.VendorId != user.VendorId) return Results.Forbid();
    return Results.Ok(award);
});

awards.MapPost("/", async (CreateAwardRequest request, ContractDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsCommittee && !user.IsTenantAdmin && !user.IsSuperAdmin) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Justification)) return Results.BadRequest("Award decision requires justification.");
    var tenantId = user.IsSuperAdmin ? request.TenantId : user.RequireTenantId();
    if (tenantId == Guid.Empty) return Results.BadRequest("TenantId is required.");

    var award = new AwardDecision
    {
        TenantId = tenantId,
        TenderId = request.TenderId,
        BidId = request.BidId,
        VendorId = request.VendorId,
        Amount = request.Amount,
        Currency = request.Currency.Trim().ToUpperInvariant(),
        Justification = request.Justification.Trim(),
        Status = AwardStatus.PendingApproval,
        DecidedByEmail = user.Email
    };
    db.AwardDecisions.Add(award);
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, tenantId, "AwardDecisionCreated", "AwardDecision", award.Id, user.Email, award.Justification);
    return Results.Created($"/api/awards/{award.Id}", award);
});

awards.MapPost("/{id:guid}/approve", async (Guid id, ApproveAwardRequest request, ContractDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsTenantAdmin && !user.IsSuperAdmin) return Results.Forbid();
    var award = await db.AwardDecisions.FindAsync(id);
    if (award is null) return Results.NotFound();
    if (!user.CanAccessTenant(award.TenantId)) return Results.Forbid();
    award.Status = request.Approved ? AwardStatus.Approved : AwardStatus.Rejected;
    award.ApprovalRemarks = request.Remarks.Trim();
    award.ApprovedByEmail = user.Email;
    award.ApprovedAtUtc = DateTime.UtcNow;
    award.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, award.TenantId, request.Approved ? "AwardApproved" : "AwardRejected", "AwardDecision", award.Id, user.Email, request.Remarks);
    return Results.Ok(award);
});

var purchaseOrders = app.MapGroup("/api/purchase-orders").RequireAuthorization();
purchaseOrders.MapGet("/", async (ContractDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var query = db.PurchaseOrders.AsQueryable();
    if (!user.IsSuperAdmin) query = query.Where(po => po.TenantId == user.RequireTenantId());
    if (user.IsVendor && user.VendorId is not null) query = query.Where(po => po.VendorId == user.VendorId);
    return Results.Ok(await query.OrderByDescending(po => po.CreatedAtUtc).ToListAsync());
});

purchaseOrders.MapGet("/{id:guid}", async (Guid id, ContractDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var po = await db.PurchaseOrders.FindAsync(id);
    if (po is null) return Results.NotFound();
    if (!user.CanAccessTenant(po.TenantId)) return Results.Forbid();
    if (user.IsVendor && po.VendorId != user.VendorId) return Results.Forbid();
    return Results.Ok(po);
});

purchaseOrders.MapPost("/generate", async (GeneratePoRequest request, ContractDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (user.Role != "Finance" && user.Role != "Procurement" && !user.IsTenantAdmin && !user.IsSuperAdmin) return Results.Forbid();
    var award = await db.AwardDecisions.FindAsync(request.AwardDecisionId);
    if (award is null) return Results.NotFound("Award decision not found.");
    if (!user.CanAccessTenant(award.TenantId)) return Results.Forbid();
    if (award.Status != AwardStatus.Approved) return Results.BadRequest("PO can be generated only after award approval.");

    var po = new PurchaseOrder
    {
        TenantId = award.TenantId,
        AwardDecisionId = award.Id,
        TenderId = award.TenderId,
        VendorId = award.VendorId,
        PoNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
        Amount = award.Amount,
        Currency = award.Currency,
        PaymentTerm = request.PaymentTerm?.Trim(),
        TaxCode = request.TaxCode?.Trim(),
        DeliveryLocation = request.DeliveryLocation?.Trim(),
        Status = PurchaseOrderStatus.Issued,
        IssuedByEmail = user.Email,
        IssuedAtUtc = DateTime.UtcNow
    };
    db.PurchaseOrders.Add(po);
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, po.TenantId, "PurchaseOrderGenerated", "PurchaseOrder", po.Id, user.Email, po.PoNumber);
    return Results.Created($"/api/purchase-orders/{po.Id}", po);
});

var contracts = app.MapGroup("/api/contracts").RequireAuthorization();
contracts.MapPost("/", async (CreateContractRequest request, ContractDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (user.IsVendor) return Results.Forbid();
    var po = await db.PurchaseOrders.FindAsync(request.PurchaseOrderId);
    if (po is null) return Results.NotFound("Purchase order not found.");
    if (!user.CanAccessTenant(po.TenantId)) return Results.Forbid();

    var contract = new Contract
    {
        TenantId = po.TenantId,
        PurchaseOrderId = po.Id,
        TenderId = po.TenderId,
        VendorId = po.VendorId,
        ContractNumber = request.ContractNumber.Trim(),
        Title = request.Title.Trim(),
        StartDateUtc = request.StartDateUtc,
        EndDateUtc = request.EndDateUtc,
        DocumentType = request.DocumentType?.Trim(),
        Value = po.Amount,
        Currency = po.Currency,
        Status = ContractStatus.Active
    };
    db.Contracts.Add(contract);
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, contract.TenantId, "ContractCreated", "Contract", contract.Id, user.Email, contract.ContractNumber);
    return Results.Created($"/api/contracts/{contract.Id}", contract);
});

contracts.MapGet("/", async (ContractDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var query = db.Contracts.AsQueryable();
    if (!user.IsSuperAdmin) query = query.Where(c => c.TenantId == user.RequireTenantId());
    if (user.IsVendor && user.VendorId is not null) query = query.Where(c => c.VendorId == user.VendorId);
    return Results.Ok(await query.OrderByDescending(c => c.CreatedAtUtc).ToListAsync());
});

contracts.MapGet("/{id:guid}", async (Guid id, ContractDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var contract = await db.Contracts.FindAsync(id);
    if (contract is null) return Results.NotFound();
    if (!user.CanAccessTenant(contract.TenantId)) return Results.Forbid();
    if (user.IsVendor && contract.VendorId != user.VendorId) return Results.Forbid();
    return Results.Ok(contract);
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContractDbContext>();
    await db.Database.EnsureCreatedAsync();
    await EnsureSchemaAsync(db);
}

app.Run();

static async Task AuditAsync(IHttpClientFactory factory, Guid tenantId, string action, string entity, Guid entityId, string actor, string details)
{
    try { await factory.CreateClient("audit").PostAsJsonAsync("/api/audit-logs", new AuditEventRequest(tenantId, "ContractService", action, entity, entityId, actor, details)); } catch { }
}

static async Task EnsureSchemaAsync(ContractDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "PurchaseOrders" ADD COLUMN IF NOT EXISTS "PaymentTerm" character varying(120) NULL;
        ALTER TABLE "PurchaseOrders" ADD COLUMN IF NOT EXISTS "TaxCode" character varying(120) NULL;
        ALTER TABLE "PurchaseOrders" ADD COLUMN IF NOT EXISTS "DeliveryLocation" character varying(160) NULL;
        ALTER TABLE "Contracts" ADD COLUMN IF NOT EXISTS "DocumentType" character varying(120) NULL;
        """);
}

public sealed class AwardDecision : TenantEntity
{
    public Guid TenderId { get; set; }
    public Guid BidId { get; set; }
    public Guid VendorId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Justification { get; set; } = string.Empty;
    public AwardStatus Status { get; set; }
    public string DecidedByEmail { get; set; } = string.Empty;
    public string? ApprovedByEmail { get; set; }
    public string? ApprovalRemarks { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
}

public sealed class PurchaseOrder : TenantEntity
{
    public Guid AwardDecisionId { get; set; }
    public Guid TenderId { get; set; }
    public Guid VendorId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? PaymentTerm { get; set; }
    public string? TaxCode { get; set; }
    public string? DeliveryLocation { get; set; }
    public PurchaseOrderStatus Status { get; set; }
    public string IssuedByEmail { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
}

public sealed class Contract : TenantEntity
{
    public Guid PurchaseOrderId { get; set; }
    public Guid TenderId { get; set; }
    public Guid VendorId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public decimal Value { get; set; }
    public string Currency { get; set; } = "USD";
    public ContractStatus Status { get; set; }
}

public enum AwardStatus { PendingApproval, Approved, Rejected }
public enum PurchaseOrderStatus { Issued, Cancelled }
public enum ContractStatus { Draft, Active, Closed, Terminated }
public sealed record CreateAwardRequest(Guid TenantId, Guid TenderId, Guid BidId, Guid VendorId, decimal Amount, string Currency, string Justification);
public sealed record ApproveAwardRequest(bool Approved, string Remarks);
public sealed record GeneratePoRequest(Guid AwardDecisionId, string? PaymentTerm, string? TaxCode, string? DeliveryLocation);
public sealed record CreateContractRequest(Guid PurchaseOrderId, string ContractNumber, string Title, string? DocumentType, DateTime StartDateUtc, DateTime EndDateUtc);

public sealed class ContractDbContext : DbContext
{
    public ContractDbContext(DbContextOptions<ContractDbContext> options) : base(options) { }
    public DbSet<AwardDecision> AwardDecisions => Set<AwardDecision>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<Contract> Contracts => Set<Contract>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AwardDecision>(e => { e.HasKey(x => x.Id); e.Property(x => x.Amount).HasPrecision(18, 2); e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32); });
        modelBuilder.Entity<PurchaseOrder>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.TenantId, x.PoNumber }).IsUnique(); e.Property(x => x.Amount).HasPrecision(18, 2); e.Property(x => x.PaymentTerm).HasMaxLength(120); e.Property(x => x.TaxCode).HasMaxLength(120); e.Property(x => x.DeliveryLocation).HasMaxLength(160); e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32); });
        modelBuilder.Entity<Contract>(e => { e.HasKey(x => x.Id); e.HasIndex(x => new { x.TenantId, x.ContractNumber }).IsUnique(); e.Property(x => x.DocumentType).HasMaxLength(120); e.Property(x => x.Value).HasPrecision(18, 2); e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32); });
    }
}
