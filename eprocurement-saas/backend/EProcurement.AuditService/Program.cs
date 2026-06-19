using System.Text;
using EProcurement.SharedKernel.Audit;
using EProcurement.SharedKernel.Entities;
using EProcurement.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AuditDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "eprocurement",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "eprocurement-web",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? "development-secret-change-me-development-secret"))
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "audit-service" }));

app.MapPost("/api/audit-logs", async (AuditEventRequest request, AuditDbContext db) =>
{
    var log = new AuditLog
    {
        TenantId = request.TenantId,
        ServiceName = request.ServiceName,
        Action = request.Action,
        EntityName = request.EntityName,
        EntityId = request.EntityId,
        ActorEmail = request.ActorEmail,
        Details = request.Details
    };
    db.AuditLogs.Add(log);
    await db.SaveChangesAsync();
    return Results.Created($"/api/audit-logs/{log.Id}", log);
});

app.MapGet("/api/audit-logs", async (Guid? tenantId, string? entityName, AuditDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var query = db.AuditLogs.AsQueryable();
    if (!user.IsSuperAdmin) query = query.Where(log => log.TenantId == user.RequireTenantId());
    if (tenantId is not null && user.IsSuperAdmin) query = query.Where(log => log.TenantId == tenantId);
    if (!string.IsNullOrWhiteSpace(entityName)) query = query.Where(log => log.EntityName == entityName);
    return Results.Ok(await query.OrderByDescending(log => log.CreatedAtUtc).Take(500).ToListAsync());
}).RequireAuthorization();

using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.EnsureCreatedAsync();
app.Run();

public sealed class AuditLog : TenantEntity
{
    public string ServiceName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string ActorEmail { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public sealed class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(log => log.Id);
            e.Property(log => log.ServiceName).HasMaxLength(80).IsRequired();
            e.Property(log => log.Action).HasMaxLength(120).IsRequired();
            e.Property(log => log.EntityName).HasMaxLength(80).IsRequired();
            e.Property(log => log.ActorEmail).HasMaxLength(256).IsRequired();
        });
    }
}
