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
builder.Services.AddDbContext<TenderDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
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
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "tender-service" }));

var group = app.MapGroup("/api/tenders").RequireAuthorization();

group.MapGet("/", async (TenderDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var query = db.Tenders.Include(t => t.InvitedVendors).AsQueryable();

    if (!user.IsSuperAdmin)
    {
        query = query.Where(t => t.TenantId == user.RequireTenantId());
    }

    if (user.IsVendor)
    {
        query = query.Where(t => t.Status == TenderStatus.Published &&
            (t.Visibility == TenderVisibility.Public || t.InvitedVendors.Any(v => v.VendorId == user.VendorId || v.VendorEmail == user.Email)));
    }

    return Results.Ok(await query.OrderByDescending(t => t.CreatedAtUtc).ToListAsync());
});

group.MapGet("/{id:guid}", async (Guid id, TenderDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var tender = await db.Tenders.Include(t => t.InvitedVendors).SingleOrDefaultAsync(t => t.Id == id);
    if (tender is null) return Results.NotFound();
    if (!user.CanAccessTenant(tender.TenantId)) return Results.Forbid();
    if (user.IsVendor && (tender.Status != TenderStatus.Published ||
        (tender.Visibility == TenderVisibility.InvitedOnly && !tender.InvitedVendors.Any(v => v.VendorId == user.VendorId || v.VendorEmail == user.Email))))
    {
        return Results.Forbid();
    }

    return Results.Ok(tender);
});

group.MapPost("/", async (CreateTenderRequest request, TenderDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (user.IsVendor) return Results.Forbid();
    var tenantId = user.IsSuperAdmin ? request.TenantId : user.RequireTenantId();
    if (tenantId == Guid.Empty) return Results.BadRequest("TenantId is required.");

    var tender = new Tender
    {
        TenantId = tenantId,
        PurchaseRequestId = request.PurchaseRequestId,
        Title = request.Title.Trim(),
        Description = request.Description.Trim(),
        Method = request.Method,
        Visibility = request.Visibility,
        EvaluationCriteria = request.EvaluationCriteria?.Trim(),
        CommitteeMember = request.CommitteeMember?.Trim(),
        DocumentType = request.DocumentType?.Trim(),
        ClosingDateUtc = request.ClosingDateUtc,
        Status = TenderStatus.Draft
    };

    db.Tenders.Add(tender);
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, tenantId, "TenderCreated", "Tender", tender.Id, user.Email, tender.Title);
    return Results.Created($"/api/tenders/{tender.Id}", tender);
});

group.MapPost("/{id:guid}/publish", async (Guid id, TenderDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (user.IsVendor) return Results.Forbid();
    var tender = await db.Tenders.FindAsync(id);
    if (tender is null) return Results.NotFound();
    if (!user.CanAccessTenant(tender.TenantId)) return Results.Forbid();
    if (tender.ClosingDateUtc <= DateTime.UtcNow) return Results.BadRequest("Closing date must be in the future.");

    tender.Status = TenderStatus.Published;
    tender.PublishedAtUtc = DateTime.UtcNow;
    tender.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, tender.TenantId, "TenderPublished", "Tender", tender.Id, user.Email, tender.Title);
    return Results.Ok(tender);
});

group.MapPost("/{id:guid}/invite", async (Guid id, InviteVendorRequest request, TenderDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (user.IsVendor) return Results.Forbid();
    var tender = await db.Tenders.Include(t => t.InvitedVendors).SingleOrDefaultAsync(t => t.Id == id);
    if (tender is null) return Results.NotFound();
    if (!user.CanAccessTenant(tender.TenantId)) return Results.Forbid();

    tender.InvitedVendors.Add(new TenderInvitation
    {
        TenantId = tender.TenantId,
        VendorId = request.VendorId,
        VendorEmail = request.VendorEmail.Trim().ToLowerInvariant()
    });
    tender.Visibility = TenderVisibility.InvitedOnly;
    tender.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, tender.TenantId, "TenderVendorInvited", "Tender", tender.Id, user.Email, request.VendorEmail);
    return Results.Ok(tender);
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TenderDbContext>();
    await db.Database.EnsureCreatedAsync();
    await EnsureSchemaAsync(db);
    await SeedDemoDataAsync(db);
}

app.Run();

static async Task AuditAsync(IHttpClientFactory factory, Guid tenantId, string action, string entity, Guid entityId, string actor, string details)
{
    try { await factory.CreateClient("audit").PostAsJsonAsync("/api/audit-logs", new AuditEventRequest(tenantId, "TenderService", action, entity, entityId, actor, details)); } catch { }
}

static async Task EnsureSchemaAsync(TenderDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "Tenders" ADD COLUMN IF NOT EXISTS "EvaluationCriteria" character varying(160) NULL;
        ALTER TABLE "Tenders" ADD COLUMN IF NOT EXISTS "CommitteeMember" character varying(160) NULL;
        ALTER TABLE "Tenders" ADD COLUMN IF NOT EXISTS "DocumentType" character varying(120) NULL;
        """);
}

static async Task SeedDemoDataAsync(TenderDbContext db)
{
    if (await db.Tenders.AnyAsync(tender => tender.Id == DemoDataIds.PublishedTenderId))
    {
        return;
    }

    db.Tenders.Add(new Tender
    {
        Id = DemoDataIds.PublishedTenderId,
        TenantId = DemoDataIds.TenantId,
        PurchaseRequestId = DemoDataIds.ApprovedPurchaseRequestId,
        Title = "RFQ for Laptop Refresh",
        Description = "Supply and delivery of business laptops, docking stations, and standard warranty support.",
        Method = TenderMethod.RFQ,
        Visibility = TenderVisibility.Public,
        EvaluationCriteria = "PRICE",
        CommitteeMember = "COM-001",
        DocumentType = "RFQ-DOC",
        Status = TenderStatus.Published,
        PublishedAtUtc = DateTime.UtcNow.AddDays(-7),
        ClosingDateUtc = DateTime.UtcNow.AddDays(-1)
    });

    await db.SaveChangesAsync();
}

public sealed class Tender : TenantEntity
{
    public Guid? PurchaseRequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TenderMethod Method { get; set; } = TenderMethod.RFQ;
    public TenderVisibility Visibility { get; set; } = TenderVisibility.Public;
    public string? EvaluationCriteria { get; set; }
    public string? CommitteeMember { get; set; }
    public string? DocumentType { get; set; }
    public TenderStatus Status { get; set; } = TenderStatus.Draft;
    public DateTime ClosingDateUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public List<TenderInvitation> InvitedVendors { get; set; } = new();
}

public sealed class TenderInvitation : TenantEntity
{
    public Guid TenderId { get; set; }
    public Guid? VendorId { get; set; }
    public string VendorEmail { get; set; } = string.Empty;
}

public enum TenderMethod { RFQ, RFP, Tender }
public enum TenderVisibility { Public, InvitedOnly }
public enum TenderStatus { Draft, Published, Closed, Awarded, Cancelled }
public sealed record CreateTenderRequest(Guid TenantId, Guid? PurchaseRequestId, string Title, string Description, TenderMethod Method, TenderVisibility Visibility, string? EvaluationCriteria, string? CommitteeMember, string? DocumentType, DateTime ClosingDateUtc);
public sealed record InviteVendorRequest(Guid? VendorId, string VendorEmail);

public sealed class TenderDbContext : DbContext
{
    public TenderDbContext(DbContextOptions<TenderDbContext> options) : base(options) { }
    public DbSet<Tender> Tenders => Set<Tender>();
    public DbSet<TenderInvitation> TenderInvitations => Set<TenderInvitation>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tender>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title).HasMaxLength(220).IsRequired();
            entity.Property(t => t.Method).HasConversion<string>().HasMaxLength(32);
            entity.Property(t => t.Visibility).HasConversion<string>().HasMaxLength(32);
            entity.Property(t => t.EvaluationCriteria).HasMaxLength(160);
            entity.Property(t => t.CommitteeMember).HasMaxLength(160);
            entity.Property(t => t.DocumentType).HasMaxLength(120);
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasMany(t => t.InvitedVendors).WithOne().HasForeignKey(i => i.TenderId);
        });
        modelBuilder.Entity<TenderInvitation>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => new { i.TenderId, i.VendorEmail }).IsUnique();
            entity.Property(i => i.VendorEmail).HasMaxLength(256).IsRequired();
        });
    }
}
