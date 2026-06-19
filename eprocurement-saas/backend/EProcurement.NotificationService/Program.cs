using System.Text;
using EProcurement.SharedKernel.Entities;
using EProcurement.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
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
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "notification-service" }));

var group = app.MapGroup("/api/notifications").RequireAuthorization();
group.MapPost("/", async (CreateNotificationRequest request, NotificationDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var tenantId = user.IsSuperAdmin ? request.TenantId : user.RequireTenantId();
    if (tenantId == Guid.Empty) return Results.BadRequest("TenantId is required.");
    var notification = new Notification
    {
        TenantId = tenantId,
        RecipientEmail = request.RecipientEmail.Trim().ToLowerInvariant(),
        Subject = request.Subject.Trim(),
        Body = request.Body.Trim(),
        Channel = request.Channel,
        Status = NotificationStatus.Pending
    };
    db.Notifications.Add(notification);
    await db.SaveChangesAsync();
    return Results.Created($"/api/notifications/{notification.Id}", notification);
});

group.MapGet("/", async (NotificationDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var query = db.Notifications.AsQueryable();
    if (!user.IsSuperAdmin) query = query.Where(n => n.TenantId == user.RequireTenantId());
    if (user.IsVendor) query = query.Where(n => n.RecipientEmail == user.Email);
    return Results.Ok(await query.OrderByDescending(n => n.CreatedAtUtc).Take(200).ToListAsync());
});

group.MapPost("/{id:guid}/mark-sent", async (Guid id, NotificationDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsSuperAdmin && !user.IsTenantAdmin) return Results.Forbid();
    var notification = await db.Notifications.FindAsync(id);
    if (notification is null) return Results.NotFound();
    if (!user.CanAccessTenant(notification.TenantId)) return Results.Forbid();
    notification.Status = NotificationStatus.Sent;
    notification.SentAtUtc = DateTime.UtcNow;
    notification.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(notification);
});

using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.EnsureCreatedAsync();
app.Run();

public sealed class Notification : TenantEntity
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; } = NotificationChannel.Email;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public DateTime? SentAtUtc { get; set; }
}

public enum NotificationChannel { Email, InApp }
public enum NotificationStatus { Pending, Sent, Failed }
public sealed record CreateNotificationRequest(Guid TenantId, string RecipientEmail, string Subject, string Body, NotificationChannel Channel);

public sealed class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }
    public DbSet<Notification> Notifications => Set<Notification>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.RecipientEmail).HasMaxLength(256).IsRequired();
            e.Property(n => n.Subject).HasMaxLength(220).IsRequired();
            e.Property(n => n.Channel).HasConversion<string>().HasMaxLength(32);
            e.Property(n => n.Status).HasConversion<string>().HasMaxLength(32);
        });
    }
}
