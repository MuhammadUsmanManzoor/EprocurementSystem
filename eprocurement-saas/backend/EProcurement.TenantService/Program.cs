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
    var items = await LoadMasterDataAsync(db, user, type);
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

    return await CreateMasterDataAsync(request.Type.Trim(), tenantId, request, db);
});

masterData.MapPut("/{id:guid}", async (Guid id, UpdateMasterDataItemRequest request, TenantDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsSuperAdmin && !user.IsTenantAdmin)
    {
        return Results.Forbid();
    }

    var result = await UpdateMasterDataAsync(id, request, db, user);
    return result ?? Results.NotFound();
});

masterData.MapDelete("/{id:guid}", async (Guid id, TenantDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsSuperAdmin && !user.IsTenantAdmin)
    {
        return Results.Forbid();
    }

    var result = await DeleteMasterDataAsync(id, db, user);
    return result ?? Results.NotFound();
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
    await TenantDbSeeder.SeedAsync(db);
}

app.Run();

static async Task<List<MasterDataItemDto>> LoadMasterDataAsync(TenantDbContext db, CurrentUser user, string? type)
{
    var items = new List<MasterDataItemDto>();
    await AddMasterDataAsync<Department>(items, db, user, type, "Department");
    await AddMasterDataAsync<CostCenter>(items, db, user, type, "CostCenter");
    await AddMasterDataAsync<Category>(items, db, user, type, "Category");
    await AddMasterDataAsync<ProcurementItem>(items, db, user, type, "Item");
    await AddMasterDataAsync<UnitOfMeasure>(items, db, user, type, "UnitOfMeasure");
    await AddMasterDataAsync<ApprovalWorkflow>(items, db, user, type, "ApprovalWorkflow");
    await AddMasterDataAsync<TenderMethodMaster>(items, db, user, type, "TenderMethod");
    await AddMasterDataAsync<EvaluationCriterion>(items, db, user, type, "EvaluationCriteria");
    await AddMasterDataAsync<CommitteeMember>(items, db, user, type, "CommitteeMember");
    await AddMasterDataAsync<Currency>(items, db, user, type, "Currency");
    await AddMasterDataAsync<TaxCode>(items, db, user, type, "TaxCode");
    await AddMasterDataAsync<PaymentTerm>(items, db, user, type, "PaymentTerm");
    await AddMasterDataAsync<DeliveryLocation>(items, db, user, type, "DeliveryLocation");
    await AddMasterDataAsync<DocumentType>(items, db, user, type, "DocumentType");
    return items.OrderBy(item => item.Type).ThenBy(item => item.Code).ToList();
}

static async Task AddMasterDataAsync<TEntity>(List<MasterDataItemDto> items, TenantDbContext db, CurrentUser user, string? typeFilter, string type)
    where TEntity : MasterDataEntity
{
    if (!string.IsNullOrWhiteSpace(typeFilter) && !type.Equals(typeFilter, StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    var query = db.Set<TEntity>().AsQueryable();
    if (!user.IsSuperAdmin)
    {
        query = query.Where(item => item.TenantId == user.RequireTenantId());
    }

    var rows = await query
        .Select(item => new MasterDataItemDto(item.Id, item.TenantId, type, item.Code, item.Name, item.Description, item.IsActive))
        .ToListAsync();

    items.AddRange(rows);
}

static Task<IResult> CreateMasterDataAsync(string type, Guid tenantId, CreateMasterDataItemRequest request, TenantDbContext db)
{
    return type switch
    {
        "Department" => CreateMasterDataEntityAsync<Department>(type, tenantId, request, db),
        "CostCenter" => CreateMasterDataEntityAsync<CostCenter>(type, tenantId, request, db),
        "Category" => CreateMasterDataEntityAsync<Category>(type, tenantId, request, db),
        "Item" => CreateMasterDataEntityAsync<ProcurementItem>(type, tenantId, request, db),
        "UnitOfMeasure" => CreateMasterDataEntityAsync<UnitOfMeasure>(type, tenantId, request, db),
        "ApprovalWorkflow" => CreateMasterDataEntityAsync<ApprovalWorkflow>(type, tenantId, request, db),
        "TenderMethod" => CreateMasterDataEntityAsync<TenderMethodMaster>(type, tenantId, request, db),
        "EvaluationCriteria" => CreateMasterDataEntityAsync<EvaluationCriterion>(type, tenantId, request, db),
        "CommitteeMember" => CreateMasterDataEntityAsync<CommitteeMember>(type, tenantId, request, db),
        "Currency" => CreateMasterDataEntityAsync<Currency>(type, tenantId, request, db),
        "TaxCode" => CreateMasterDataEntityAsync<TaxCode>(type, tenantId, request, db),
        "PaymentTerm" => CreateMasterDataEntityAsync<PaymentTerm>(type, tenantId, request, db),
        "DeliveryLocation" => CreateMasterDataEntityAsync<DeliveryLocation>(type, tenantId, request, db),
        "DocumentType" => CreateMasterDataEntityAsync<DocumentType>(type, tenantId, request, db),
        _ => Task.FromResult<IResult>(Results.BadRequest($"Unsupported master data type '{type}'."))
    };
}

static async Task<IResult> CreateMasterDataEntityAsync<TEntity>(string type, Guid tenantId, CreateMasterDataItemRequest request, TenantDbContext db)
    where TEntity : MasterDataEntity, new()
{
    var item = new TEntity
    {
        TenantId = tenantId,
        Code = request.Code.Trim().ToUpperInvariant(),
        Name = request.Name.Trim(),
        Description = request.Description?.Trim(),
        IsActive = true
    };

    db.Set<TEntity>().Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/master-data/{item.Id}", ToDto(type, item));
}

static async Task<IResult?> UpdateMasterDataAsync(Guid id, UpdateMasterDataItemRequest request, TenantDbContext db, CurrentUser user)
{
    return await UpdateMasterDataEntityAsync<Department>(id, request, db, user, "Department")
        ?? await UpdateMasterDataEntityAsync<CostCenter>(id, request, db, user, "CostCenter")
        ?? await UpdateMasterDataEntityAsync<Category>(id, request, db, user, "Category")
        ?? await UpdateMasterDataEntityAsync<ProcurementItem>(id, request, db, user, "Item")
        ?? await UpdateMasterDataEntityAsync<UnitOfMeasure>(id, request, db, user, "UnitOfMeasure")
        ?? await UpdateMasterDataEntityAsync<ApprovalWorkflow>(id, request, db, user, "ApprovalWorkflow")
        ?? await UpdateMasterDataEntityAsync<TenderMethodMaster>(id, request, db, user, "TenderMethod")
        ?? await UpdateMasterDataEntityAsync<EvaluationCriterion>(id, request, db, user, "EvaluationCriteria")
        ?? await UpdateMasterDataEntityAsync<CommitteeMember>(id, request, db, user, "CommitteeMember")
        ?? await UpdateMasterDataEntityAsync<Currency>(id, request, db, user, "Currency")
        ?? await UpdateMasterDataEntityAsync<TaxCode>(id, request, db, user, "TaxCode")
        ?? await UpdateMasterDataEntityAsync<PaymentTerm>(id, request, db, user, "PaymentTerm")
        ?? await UpdateMasterDataEntityAsync<DeliveryLocation>(id, request, db, user, "DeliveryLocation")
        ?? await UpdateMasterDataEntityAsync<DocumentType>(id, request, db, user, "DocumentType");
}

static async Task<IResult?> UpdateMasterDataEntityAsync<TEntity>(Guid id, UpdateMasterDataItemRequest request, TenantDbContext db, CurrentUser user, string type)
    where TEntity : MasterDataEntity
{
    var item = await db.Set<TEntity>().FindAsync(id);
    if (item is null)
    {
        return null;
    }

    if (!user.CanAccessTenant(item.TenantId))
    {
        return Results.Forbid();
    }

    item.Code = request.Code.Trim().ToUpperInvariant();
    item.Name = request.Name.Trim();
    item.Description = request.Description?.Trim();
    item.IsActive = request.IsActive;
    item.UpdatedAtUtc = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(ToDto(type, item));
}

static async Task<IResult?> DeleteMasterDataAsync(Guid id, TenantDbContext db, CurrentUser user)
{
    return await DeleteMasterDataEntityAsync<Department>(id, db, user)
        ?? await DeleteMasterDataEntityAsync<CostCenter>(id, db, user)
        ?? await DeleteMasterDataEntityAsync<Category>(id, db, user)
        ?? await DeleteMasterDataEntityAsync<ProcurementItem>(id, db, user)
        ?? await DeleteMasterDataEntityAsync<UnitOfMeasure>(id, db, user)
        ?? await DeleteMasterDataEntityAsync<ApprovalWorkflow>(id, db, user)
        ?? await DeleteMasterDataEntityAsync<TenderMethodMaster>(id, db, user)
        ?? await DeleteMasterDataEntityAsync<EvaluationCriterion>(id, db, user)
        ?? await DeleteMasterDataEntityAsync<CommitteeMember>(id, db, user)
        ?? await DeleteMasterDataEntityAsync<Currency>(id, db, user)
        ?? await DeleteMasterDataEntityAsync<TaxCode>(id, db, user)
        ?? await DeleteMasterDataEntityAsync<PaymentTerm>(id, db, user)
        ?? await DeleteMasterDataEntityAsync<DeliveryLocation>(id, db, user)
        ?? await DeleteMasterDataEntityAsync<DocumentType>(id, db, user);
}

static async Task<IResult?> DeleteMasterDataEntityAsync<TEntity>(Guid id, TenantDbContext db, CurrentUser user)
    where TEntity : MasterDataEntity
{
    var item = await db.Set<TEntity>().FindAsync(id);
    if (item is null)
    {
        return null;
    }

    if (!user.CanAccessTenant(item.TenantId))
    {
        return Results.Forbid();
    }

    db.Set<TEntity>().Remove(item);
    await db.SaveChangesAsync();
    return Results.NoContent();
}

static MasterDataItemDto ToDto(string type, MasterDataEntity item)
{
    return new MasterDataItemDto(item.Id, item.TenantId, type, item.Code, item.Name, item.Description, item.IsActive);
}

public sealed record MasterDataItemDto(Guid Id, Guid TenantId, string Type, string Code, string Name, string? Description, bool IsActive);
public sealed record CreateMasterDataItemRequest(Guid TenantId, string Type, string Code, string Name, string? Description);
public sealed record UpdateMasterDataItemRequest(string Code, string Name, string? Description, bool IsActive);
