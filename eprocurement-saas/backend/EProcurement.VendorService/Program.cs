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

builder.Services.AddDbContext<VendorDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient("audit", client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:Audit"] ?? "http://audit-service:8080"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
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
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "vendor-service" }));

var group = app.MapGroup("/api/vendors").RequireAuthorization();

group.MapGet("/", async (VendorDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var query = db.Vendors.AsQueryable();
    if (!user.IsSuperAdmin)
    {
        query = query.Where(vendor => vendor.TenantId == user.RequireTenantId());
    }

    if (user.IsVendor)
    {
        query = query.Where(vendor => vendor.ContactEmail == user.Email || vendor.Id == user.VendorId);
    }

    return Results.Ok(await query.OrderBy(vendor => vendor.Name).ToListAsync());
});

group.MapGet("/{id:guid}", async (Guid id, VendorDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var vendor = await db.Vendors.FindAsync(id);
    if (vendor is null) return Results.NotFound();
    if (!user.CanAccessTenant(vendor.TenantId)) return Results.Forbid();
    if (user.IsVendor && vendor.ContactEmail != user.Email && vendor.Id != user.VendorId) return Results.Forbid();
    return Results.Ok(vendor);
});

group.MapPost("/register", async (RegisterVendorRequest request, VendorDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var tenantId = user.IsSuperAdmin ? request.TenantId : user.RequireTenantId();
    if (tenantId == Guid.Empty)
    {
        return Results.BadRequest("TenantId is required.");
    }

    var vendor = new Vendor
    {
        TenantId = tenantId,
        Name = request.Name.Trim(),
        RegistrationNumber = request.RegistrationNumber.Trim(),
        ContactEmail = request.ContactEmail.Trim().ToLowerInvariant(),
        ContactPhone = request.ContactPhone.Trim(),
        Status = VendorStatus.Pending
    };

    db.Vendors.Add(vendor);
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, tenantId, "VendorRegistered", "Vendor", vendor.Id, user.Email, $"Vendor {vendor.Name} registered.");
    return Results.Created($"/api/vendors/{vendor.Id}", vendor);
});

group.MapPost("/{id:guid}/approve", async (Guid id, ApproveVendorRequest request, VendorDbContext db, IHttpClientFactory httpClientFactory, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsSuperAdmin && !user.IsTenantAdmin)
    {
        return Results.Forbid();
    }

    var vendor = await db.Vendors.FindAsync(id);
    if (vendor is null)
    {
        return Results.NotFound();
    }

    if (!user.CanAccessTenant(vendor.TenantId))
    {
        return Results.Forbid();
    }

    vendor.Status = request.Approved ? VendorStatus.Approved : VendorStatus.Rejected;
    vendor.ApprovalRemarks = request.Remarks;
    vendor.ApprovedAtUtc = DateTime.UtcNow;
    vendor.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    await AuditAsync(httpClientFactory, vendor.TenantId, request.Approved ? "VendorApproved" : "VendorRejected", "Vendor", vendor.Id, user.Email, request.Remarks);
    return Results.Ok(vendor);
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedDemoDataAsync(db);
}

app.Run();

static async Task AuditAsync(IHttpClientFactory factory, Guid tenantId, string action, string entity, Guid entityId, string actor, string details)
{
    try
    {
        await factory.CreateClient("audit").PostAsJsonAsync("/api/audit-logs", new AuditEventRequest(tenantId, "VendorService", action, entity, entityId, actor, details));
    }
    catch
    {
        // Audit service may be unavailable during local startup; retry/outbox can be added later.
    }
}

static async Task SeedDemoDataAsync(VendorDbContext db)
{
    if (await db.Vendors.AnyAsync(vendor => vendor.Id == DemoDataIds.VendorAId))
    {
        return;
    }

    db.Vendors.AddRange(
        new Vendor
        {
            Id = DemoDataIds.VendorAId,
            TenantId = DemoDataIds.TenantId,
            Name = "Vendor A Technologies",
            RegistrationNumber = "VENDOR-A-001",
            ContactEmail = "vendor.a@demo.com",
            ContactPhone = "+603-5550-0101",
            Status = VendorStatus.Approved,
            ApprovalRemarks = "Seed vendor approved for MVP bidding.",
            ApprovedAtUtc = DateTime.UtcNow.AddDays(-9)
        },
        new Vendor
        {
            Id = DemoDataIds.VendorBId,
            TenantId = DemoDataIds.TenantId,
            Name = "Vendor B Supplies",
            RegistrationNumber = "VENDOR-B-001",
            ContactEmail = "vendor.b@demo.com",
            ContactPhone = "+603-5550-0102",
            Status = VendorStatus.Approved,
            ApprovalRemarks = "Seed vendor approved for MVP bidding.",
            ApprovedAtUtc = DateTime.UtcNow.AddDays(-9)
        },
        new Vendor
        {
            Id = DemoDataIds.VendorCId,
            TenantId = DemoDataIds.TenantId,
            Name = "Vendor C Solutions",
            RegistrationNumber = "VENDOR-C-001",
            ContactEmail = "vendor.c@demo.com",
            ContactPhone = "+603-5550-0103",
            Status = VendorStatus.Approved,
            ApprovalRemarks = "Seed vendor approved for MVP bidding.",
            ApprovedAtUtc = DateTime.UtcNow.AddDays(-9)
        });

    await db.SaveChangesAsync();
}

public sealed class Vendor : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public VendorStatus Status { get; set; } = VendorStatus.Pending;
    public string? ApprovalRemarks { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
}

public enum VendorStatus { Pending, Approved, Rejected, Suspended }

public sealed record RegisterVendorRequest(Guid TenantId, string Name, string RegistrationNumber, string ContactEmail, string ContactPhone);
public sealed record ApproveVendorRequest(bool Approved, string Remarks);

public sealed class VendorDbContext : DbContext
{
    public VendorDbContext(DbContextOptions<VendorDbContext> options) : base(options) { }
    public DbSet<Vendor> Vendors => Set<Vendor>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.HasKey(vendor => vendor.Id);
            entity.HasIndex(vendor => new { vendor.TenantId, vendor.RegistrationNumber }).IsUnique();
            entity.Property(vendor => vendor.Name).HasMaxLength(180).IsRequired();
            entity.Property(vendor => vendor.RegistrationNumber).HasMaxLength(80).IsRequired();
            entity.Property(vendor => vendor.ContactEmail).HasMaxLength(256).IsRequired();
            entity.Property(vendor => vendor.Status).HasConversion<string>().HasMaxLength(32);
        });
    }
}
