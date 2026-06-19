using System.Text;
using EProcurement.Contracts.Tenants;
using EProcurement.SharedKernel.Security;
using EProcurement.TenantService.Data;
using EProcurement.TenantService.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TenantDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration["Frontend:Url"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
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

var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "tenant-service" }));

app.MapGet("/api/tenants", async (TenantDbContext db) =>
{
    var tenants = await db.Tenants
        .OrderBy(tenant => tenant.Name)
        .Select(tenant => new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive))
        .ToListAsync();

    return Results.Ok(tenants);
});

app.MapGet("/api/tenants/{id:guid}", async (Guid id, TenantDbContext db) =>
{
    var tenant = await db.Tenants.FindAsync(id);
    return tenant is null
        ? Results.NotFound()
        : Results.Ok(new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive));
});

app.MapPost("/api/tenants", async (CreateTenantRequest request, TenantDbContext db) =>
{
    var tenant = new Tenant
    {
        Name = request.Name.Trim(),
        Slug = request.Slug.Trim().ToLowerInvariant(),
        IsActive = true
    };

    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();

    return Results.Created($"/api/tenants/{tenant.Id}", new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive));
});

app.MapPut("/api/tenants/{id:guid}", async (Guid id, UpdateTenantRequest request, TenantDbContext db) =>
{
    var tenant = await db.Tenants.FindAsync(id);
    if (tenant is null)
    {
        return Results.NotFound();
    }

    tenant.Name = request.Name.Trim();
    tenant.Slug = request.Slug.Trim().ToLowerInvariant();
    tenant.IsActive = request.IsActive;
    tenant.UpdatedAtUtc = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive));
});

app.MapDelete("/api/tenants/{id:guid}", async (Guid id, TenantDbContext db) =>
{
    var tenant = await db.Tenants.FindAsync(id);
    if (tenant is null)
    {
        return Results.NotFound();
    }

    db.Tenants.Remove(tenant);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

var masterData = app.MapGroup("/api/master-data").RequireAuthorization();

masterData.MapGet("/", async (string? type, TenantDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    var query = db.MasterDataItems.AsQueryable();
    if (!user.IsSuperAdmin)
    {
        query = query.Where(item => item.TenantId == user.RequireTenantId());
    }

    if (!string.IsNullOrWhiteSpace(type))
    {
        query = query.Where(item => item.Type == type);
    }

    var items = await query
        .OrderBy(item => item.Type)
        .ThenBy(item => item.Code)
        .Select(item => new MasterDataItemDto(item.Id, item.TenantId, item.Type, item.Code, item.Name, item.Description, item.IsActive))
        .ToListAsync();

    return Results.Ok(items);
});

masterData.MapPost("/", async (CreateMasterDataItemRequest request, TenantDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsSuperAdmin && !user.IsTenantAdmin)
    {
        return Results.Forbid();
    }

    var tenantId = user.IsSuperAdmin ? request.TenantId : user.RequireTenantId();
    if (tenantId == Guid.Empty)
    {
        return Results.BadRequest("TenantId is required.");
    }

    var item = new MasterDataItem
    {
        TenantId = tenantId,
        Type = request.Type.Trim(),
        Code = request.Code.Trim().ToUpperInvariant(),
        Name = request.Name.Trim(),
        Description = request.Description?.Trim(),
        IsActive = true
    };

    db.MasterDataItems.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/master-data/{item.Id}", new MasterDataItemDto(item.Id, item.TenantId, item.Type, item.Code, item.Name, item.Description, item.IsActive));
});

masterData.MapPut("/{id:guid}", async (Guid id, UpdateMasterDataItemRequest request, TenantDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsSuperAdmin && !user.IsTenantAdmin)
    {
        return Results.Forbid();
    }

    var item = await db.MasterDataItems.FindAsync(id);
    if (item is null) return Results.NotFound();
    if (!user.CanAccessTenant(item.TenantId)) return Results.Forbid();

    item.Code = request.Code.Trim().ToUpperInvariant();
    item.Name = request.Name.Trim();
    item.Description = request.Description?.Trim();
    item.IsActive = request.IsActive;
    item.UpdatedAtUtc = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(new MasterDataItemDto(item.Id, item.TenantId, item.Type, item.Code, item.Name, item.Description, item.IsActive));
});

masterData.MapDelete("/{id:guid}", async (Guid id, TenantDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsSuperAdmin && !user.IsTenantAdmin)
    {
        return Results.Forbid();
    }

    var item = await db.MasterDataItems.FindAsync(id);
    if (item is null) return Results.NotFound();
    if (!user.CanAccessTenant(item.TenantId)) return Results.Forbid();

    db.MasterDataItems.Remove(item);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
    await TenantDbSeeder.SeedAsync(db);
}

app.Run();

public sealed record MasterDataItemDto(Guid Id, Guid TenantId, string Type, string Code, string Name, string? Description, bool IsActive);
public sealed record CreateMasterDataItemRequest(Guid TenantId, string Type, string Code, string Name, string? Description);
public sealed record UpdateMasterDataItemRequest(string Code, string Name, string? Description, bool IsActive);
