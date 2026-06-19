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
builder.Services.AddDbContext<BiddingDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
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
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "bidding-service" }));

var group = app.MapGroup("/api/bids").RequireAuthorization();

group.MapGet("/", async (Guid? tenderId, BiddingDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var query = db.Bids.Include(b => b.Items).AsQueryable();
    if (!user.IsSuperAdmin)
    {
        query = query.Where(b => b.TenantId == user.RequireTenantId());
    }

    if (user.IsVendor)
    {
        query = query.Where(b => b.VendorId == user.VendorId || b.VendorEmail == user.Email);
    }

    if (tenderId is not null)
    {
        query = query.Where(b => b.TenderId == tenderId);
    }

    return Results.Ok(await query.OrderByDescending(b => b.CreatedAtUtc).ToListAsync());
});

group.MapGet("/{id:guid}", async (Guid id, BiddingDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var bid = await db.Bids.Include(b => b.Items).SingleOrDefaultAsync(b => b.Id == id);
    if (bid is null) return Results.NotFound();
    if (!user.CanAccessTenant(bid.TenantId)) return Results.Forbid();
    if (user.IsVendor && bid.VendorId != user.VendorId && bid.VendorEmail != user.Email) return Results.Forbid();
    return Results.Ok(bid);
});

group.MapPost("/", async (SubmitBidRequest request, BiddingDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsVendor) return Results.Forbid();
    var tenantId = user.RequireTenantId();
    if (request.TenderClosingDateUtc <= DateTime.UtcNow) return Results.BadRequest("Tender is closed. Bids cannot be submitted or revised.");

    var vendorId = request.VendorId ?? user.VendorId;
    if (vendorId is null) return Results.BadRequest("VendorId is required for vendor users.");

    var bid = await db.Bids.Include(b => b.Items)
        .SingleOrDefaultAsync(b => b.TenantId == tenantId && b.TenderId == request.TenderId && b.VendorId == vendorId);

    if (bid is null)
    {
        bid = new Bid
        {
            TenantId = tenantId,
            TenderId = request.TenderId,
            TenderClosingDateUtc = request.TenderClosingDateUtc,
            VendorId = vendorId.Value,
            VendorEmail = user.Email,
            Status = BidStatus.Submitted
        };
        db.Bids.Add(bid);
    }
    else
    {
        bid.Status = BidStatus.Revised;
        bid.RevisionNumber++;
        bid.Items.Clear();
        bid.UpdatedAtUtc = DateTime.UtcNow;
    }

    bid.Currency = request.Currency.Trim().ToUpperInvariant();
    bid.TotalAmount = request.Items.Sum(item => item.Quantity * item.UnitPrice);
    bid.Items = request.Items.Select(item => new BidItem
    {
        TenantId = tenantId,
        Description = item.Description.Trim(),
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice
    }).ToList();

    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, tenantId, bid.RevisionNumber == 0 ? "BidSubmitted" : "BidRevised", "Bid", bid.Id, user.Email, $"Tender {bid.TenderId}");
    return Results.Ok(new { bid.Id, bid.Status, bid.RevisionNumber, bid.TotalAmount, bid.Currency });
});

group.MapPost("/tenders/{tenderId:guid}/open", async (Guid tenderId, OpenBidsRequest request, BiddingDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsCommittee && !user.IsSuperAdmin) return Results.Forbid();
    if (request.TenderClosingDateUtc > DateTime.UtcNow) return Results.BadRequest("Bids can be opened only after tender closing date.");

    var tenantId = user.RequireTenantId();
    var bids = await db.Bids.Where(b => b.TenantId == tenantId && b.TenderId == tenderId).ToListAsync();
    foreach (var bid in bids)
    {
        bid.OpenedAtUtc = DateTime.UtcNow;
        bid.OpenedByEmail = user.Email;
        bid.Status = BidStatus.Opened;
    }

    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, tenantId, "BidsOpened", "Tender", tenderId, user.Email, $"{bids.Count} bids opened.");
    return Results.Ok(new { TenderId = tenderId, OpenedCount = bids.Count });
});

group.MapGet("/tenders/{tenderId:guid}/comparison", async (Guid tenderId, DateTime tenderClosingDateUtc, BiddingDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var tenantId = user.RequireTenantId();
    var bids = await db.Bids.Where(b => b.TenantId == tenantId && b.TenderId == tenderId).OrderBy(b => b.TotalAmount).ToListAsync();
    var bidsOpened = bids.Any() && bids.All(b => b.OpenedAtUtc is not null);
    var canSeePrices = tenderClosingDateUtc <= DateTime.UtcNow && bidsOpened && (user.IsCommittee || user.IsSuperAdmin || user.Role == "Procurement" || user.IsTenantAdmin);
    var lowestBidId = canSeePrices ? bids.FirstOrDefault()?.Id : null;

    var comparison = bids.Select(bid => new
    {
        bid.Id,
        bid.VendorId,
        bid.VendorEmail,
        bid.Status,
        bid.Currency,
        TotalAmount = canSeePrices ? bid.TotalAmount : (decimal?)null,
        IsPriceVisible = canSeePrices,
        IsLowest = canSeePrices && bid.Id == lowestBidId
    });

    return Results.Ok(comparison);
});

group.MapPost("/{id:guid}/evaluate", async (Guid id, EvaluateBidRequest request, BiddingDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsCommittee && !user.IsSuperAdmin) return Results.Forbid();
    var bid = await db.Bids.FindAsync(id);
    if (bid is null) return Results.NotFound();
    if (!user.CanAccessTenant(bid.TenantId)) return Results.Forbid();
    if (bid.OpenedAtUtc is null) return Results.BadRequest("Bid must be opened before evaluation.");

    bid.TechnicalScore = request.TechnicalScore;
    bid.FinancialScore = request.FinancialScore;
    bid.EvaluationRemarks = request.Remarks.Trim();
    bid.Status = BidStatus.Evaluated;
    bid.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, bid.TenantId, "BidEvaluated", "Bid", bid.Id, user.Email, request.Remarks);
    return Results.Ok(bid);
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BiddingDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedDemoDataAsync(db);
}

app.Run();

static async Task AuditAsync(IHttpClientFactory factory, Guid tenantId, string action, string entity, Guid entityId, string actor, string details)
{
    try { await factory.CreateClient("audit").PostAsJsonAsync("/api/audit-logs", new AuditEventRequest(tenantId, "BiddingService", action, entity, entityId, actor, details)); } catch { }
}

static async Task SeedDemoDataAsync(BiddingDbContext db)
{
    if (await db.Bids.AnyAsync(bid => bid.Id == DemoDataIds.VendorABidId))
    {
        return;
    }

    var closingDate = DateTime.UtcNow.AddDays(-1);
    db.Bids.AddRange(
        CreateSeedBid(DemoDataIds.VendorABidId, DemoDataIds.VendorAId, "vendor.a@demo.com", 50000, closingDate),
        CreateSeedBid(DemoDataIds.VendorBBidId, DemoDataIds.VendorBId, "vendor.b@demo.com", 60000, closingDate),
        CreateSeedBid(DemoDataIds.VendorCBidId, DemoDataIds.VendorCId, "vendor.c@demo.com", 55000, closingDate));

    await db.SaveChangesAsync();
}

static Bid CreateSeedBid(Guid id, Guid vendorId, string vendorEmail, decimal amount, DateTime closingDate)
{
    return new Bid
    {
        Id = id,
        TenantId = DemoDataIds.TenantId,
        TenderId = DemoDataIds.PublishedTenderId,
        TenderClosingDateUtc = closingDate,
        VendorId = vendorId,
        VendorEmail = vendorEmail,
        Currency = "USD",
        TotalAmount = amount,
        Status = BidStatus.Submitted,
        Items = new List<BidItem>
        {
            new()
            {
                TenantId = DemoDataIds.TenantId,
                Description = "Business laptop bundle",
                Quantity = 10,
                UnitPrice = amount / 10
            }
        }
    };
}

public sealed class Bid : TenantEntity
{
    public Guid TenderId { get; set; }
    public DateTime TenderClosingDateUtc { get; set; }
    public Guid VendorId { get; set; }
    public string VendorEmail { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public decimal TotalAmount { get; set; }
    public int RevisionNumber { get; set; }
    public BidStatus Status { get; set; } = BidStatus.Submitted;
    public DateTime? OpenedAtUtc { get; set; }
    public string? OpenedByEmail { get; set; }
    public decimal? TechnicalScore { get; set; }
    public decimal? FinancialScore { get; set; }
    public string? EvaluationRemarks { get; set; }
    public List<BidItem> Items { get; set; } = new();
}

public sealed class BidItem : TenantEntity
{
    public Guid BidId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public enum BidStatus { Submitted, Revised, Opened, Evaluated, Disqualified }
public sealed record SubmitBidRequest(Guid TenderId, Guid? VendorId, DateTime TenderClosingDateUtc, string Currency, List<SubmitBidItem> Items);
public sealed record SubmitBidItem(string Description, decimal Quantity, decimal UnitPrice);
public sealed record OpenBidsRequest(DateTime TenderClosingDateUtc);
public sealed record EvaluateBidRequest(decimal TechnicalScore, decimal FinancialScore, string Remarks);

public sealed class BiddingDbContext : DbContext
{
    public BiddingDbContext(DbContextOptions<BiddingDbContext> options) : base(options) { }
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<BidItem> BidItems => Set<BidItem>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bid>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.HasIndex(b => new { b.TenantId, b.TenderId, b.VendorId }).IsUnique();
            entity.Property(b => b.TotalAmount).HasPrecision(18, 2);
            entity.Property(b => b.TechnicalScore).HasPrecision(8, 2);
            entity.Property(b => b.FinancialScore).HasPrecision(8, 2);
            entity.Property(b => b.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasMany(b => b.Items).WithOne().HasForeignKey(i => i.BidId);
        });
        modelBuilder.Entity<BidItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Quantity).HasPrecision(18, 2);
            entity.Property(i => i.UnitPrice).HasPrecision(18, 2);
        });
    }
}
