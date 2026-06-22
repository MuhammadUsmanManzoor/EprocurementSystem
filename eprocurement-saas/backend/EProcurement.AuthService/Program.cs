using EProcurement.AuthService.Data;
using EProcurement.AuthService.Models;
using EProcurement.AuthService.Security;
using EProcurement.Contracts.Auth;
using EProcurement.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<JwtTokenFactory>();
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
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration["Frontend:Url"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "auth-service" }));

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    AuthDbContext db,
    PasswordHasher passwordHasher,
    JwtTokenFactory tokenFactory) =>
{
    var user = await db.Users.SingleOrDefaultAsync(item => item.Email == request.Email.ToLower());
    if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var token = tokenFactory.Create(user);
    var dto = new AuthenticatedUserDto(user.Id, user.TenantId, user.Email, user.FullName, user.Role);
    return Results.Ok(new LoginResponse(token, dto));
});

var users = app.MapGroup("/api/users").RequireAuthorization();

users.MapGet("/", async (AuthDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!CanManageSecurity(user)) return Results.Forbid();

    var query = db.Users.AsQueryable();
    if (!user.IsSuperAdmin)
    {
        query = query.Where(item => item.TenantId == user.RequireTenantId());
    }

    var rows = await query
        .OrderBy(item => item.FullName)
        .Select(item => new UserAdminDto(item.Id, item.TenantId, item.Email, item.FullName, item.Role, item.IsActive, item.CreatedAtUtc))
        .ToListAsync();
    return Results.Ok(rows);
});

users.MapPost("/", async (CreateUserRequest request, AuthDbContext db, PasswordHasher hasher, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!CanManageSecurity(user)) return Results.Forbid();

    var tenantId = user.IsSuperAdmin ? request.TenantId : user.RequireTenantId();
    var role = await FindAccessibleRoleAsync(db, request.Role, tenantId, user);
    if (role is null) return Results.BadRequest("Selected role is not available for this tenant.");
    if (role.Code != "SuperAdmin" && tenantId is null) return Results.BadRequest("TenantId is required for tenant users.");
    var targetTenantId = tenantId.GetValueOrDefault();

    var email = request.Email.Trim().ToLowerInvariant();
    if (await db.Users.AnyAsync(item => item.Email == email)) return Results.BadRequest("Email already exists.");

    var item = new AppUser
    {
        TenantId = role.Code == "SuperAdmin" ? null : targetTenantId,
        Email = email,
        FullName = request.FullName.Trim(),
        Role = role.Code,
        PasswordHash = hasher.Hash(string.IsNullOrWhiteSpace(request.Password) ? "Password123!" : request.Password),
        IsActive = true
    };

    db.Users.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{item.Id}", new UserAdminDto(item.Id, item.TenantId, item.Email, item.FullName, item.Role, item.IsActive, item.CreatedAtUtc));
});

users.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, AuthDbContext db, PasswordHasher hasher, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!CanManageSecurity(user)) return Results.Forbid();

    var item = await db.Users.FindAsync(id);
    if (item is null) return Results.NotFound();
    if (!user.IsSuperAdmin && item.TenantId != user.RequireTenantId()) return Results.Forbid();

    var tenantId = user.IsSuperAdmin ? request.TenantId : user.RequireTenantId();
    var role = await FindAccessibleRoleAsync(db, request.Role, tenantId, user);
    if (role is null) return Results.BadRequest("Selected role is not available for this tenant.");
    if (role.Code != "SuperAdmin" && tenantId is null) return Results.BadRequest("TenantId is required for tenant users.");
    var targetTenantId = tenantId.GetValueOrDefault();

    item.FullName = request.FullName.Trim();
    item.Role = role.Code;
    item.TenantId = role.Code == "SuperAdmin" ? null : targetTenantId;
    item.IsActive = request.IsActive;
    item.UpdatedAtUtc = DateTime.UtcNow;
    if (!string.IsNullOrWhiteSpace(request.Password))
    {
        item.PasswordHash = hasher.Hash(request.Password);
    }

    await db.SaveChangesAsync();
    return Results.Ok(new UserAdminDto(item.Id, item.TenantId, item.Email, item.FullName, item.Role, item.IsActive, item.CreatedAtUtc));
});

var roles = app.MapGroup("/api/roles").RequireAuthorization();

roles.MapGet("/", async (AuthDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!CanManageSecurity(user)) return Results.Forbid();

    var query = db.Roles.Include(role => role.Permissions).AsQueryable();
    if (!user.IsSuperAdmin)
    {
        var tenantId = user.RequireTenantId();
        query = query.Where(role => role.TenantId == tenantId);
    }

    var roleRows = await query
        .OrderBy(role => role.Name)
        .ToListAsync();
    return Results.Ok(roleRows.Select(ToRoleDto).ToList());
});

roles.MapPost("/", async (CreateRoleRequest request, AuthDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!CanManageSecurity(user)) return Results.Forbid();

    var tenantId = user.IsSuperAdmin ? request.TenantId : user.RequireTenantId();
    var code = request.Code.Trim();
    if (await db.Roles.AnyAsync(role => role.TenantId == tenantId && role.Code == code)) return Results.BadRequest("Role code already exists.");

    var role = new AppRole
    {
        TenantId = tenantId,
        Code = code,
        Name = request.Name.Trim(),
        Description = request.Description?.Trim() ?? string.Empty,
        IsSystem = false,
        IsActive = true,
        Permissions = request.Permissions.Select(permission => FromPermissionDto(tenantId, permission)).ToList()
    };

    db.Roles.Add(role);
    await db.SaveChangesAsync();
    return Results.Created($"/api/roles/{role.Id}", ToRoleDto(role));
});

roles.MapPut("/{id:guid}", async (Guid id, UpdateRoleRequest request, AuthDbContext db, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!CanManageSecurity(user)) return Results.Forbid();

    var role = await db.Roles.Include(item => item.Permissions).SingleOrDefaultAsync(item => item.Id == id);
    if (role is null) return Results.NotFound();
    if (!user.IsSuperAdmin && role.TenantId != user.RequireTenantId()) return Results.Forbid();

    role.Name = request.Name.Trim();
    role.Description = request.Description?.Trim() ?? string.Empty;
    role.IsActive = request.IsActive;
    role.UpdatedAtUtc = DateTime.UtcNow;

    foreach (var dto in request.Permissions)
    {
        var permission = role.Permissions.FirstOrDefault(item => item.Module == dto.Module && item.Scenario == dto.Scenario);
        if (permission is null)
        {
            role.Permissions.Add(FromPermissionDto(role.TenantId, dto));
            continue;
        }

        ApplyPermission(permission, dto);
    }

    await db.SaveChangesAsync();
    return Results.Ok(ToRoleDto(role));
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();
    await AuthDbSeeder.SeedAsync(db, hasher);
}

app.Run();

static bool CanManageSecurity(CurrentUser user) => user.IsSuperAdmin || user.IsTenantAdmin;

static async Task<AppRole?> FindAccessibleRoleAsync(AuthDbContext db, string code, Guid? tenantId, CurrentUser user)
{
    var role = await db.Roles.SingleOrDefaultAsync(item => item.Code == code && (item.TenantId == tenantId || item.TenantId == null));
    if (role is null) return null;
    if (!user.IsSuperAdmin && role.Code == "SuperAdmin") return null;
    if (!user.IsSuperAdmin && role.TenantId != tenantId) return null;
    return role;
}

static RoleAdminDto ToRoleDto(AppRole role)
{
    return new RoleAdminDto(
        role.Id,
        role.TenantId,
        role.Code,
        role.Name,
        role.Description,
        role.IsSystem,
        role.IsActive,
        role.Permissions.OrderBy(item => item.Module).ThenBy(item => item.Scenario).Select(ToPermissionDto).ToList());
}

static RolePermissionDto ToPermissionDto(RolePermission permission)
{
    return new RolePermissionDto(permission.Id, permission.Module, permission.Scenario, permission.CanView, permission.CanCreate, permission.CanEdit, permission.CanDelete, permission.CanSubmit, permission.CanApprove, permission.CanOpen, permission.CanEvaluate, permission.CanAward, permission.CanGenerate, permission.CanExport, permission.CanAudit);
}

static RolePermission FromPermissionDto(Guid? tenantId, RolePermissionDto dto)
{
    var permission = new RolePermission
    {
        TenantId = tenantId,
        Module = dto.Module,
        Scenario = dto.Scenario
    };
    ApplyPermission(permission, dto);
    return permission;
}

static void ApplyPermission(RolePermission permission, RolePermissionDto dto)
{
    permission.CanView = dto.CanView;
    permission.CanCreate = dto.CanCreate;
    permission.CanEdit = dto.CanEdit;
    permission.CanDelete = dto.CanDelete;
    permission.CanSubmit = dto.CanSubmit;
    permission.CanApprove = dto.CanApprove;
    permission.CanOpen = dto.CanOpen;
    permission.CanEvaluate = dto.CanEvaluate;
    permission.CanAward = dto.CanAward;
    permission.CanGenerate = dto.CanGenerate;
    permission.CanExport = dto.CanExport;
    permission.CanAudit = dto.CanAudit;
    permission.UpdatedAtUtc = DateTime.UtcNow;
}

public sealed record UserAdminDto(Guid Id, Guid? TenantId, string Email, string FullName, string Role, bool IsActive, DateTime CreatedAtUtc);
public sealed record CreateUserRequest(Guid? TenantId, string Email, string FullName, string Role, string? Password);
public sealed record UpdateUserRequest(Guid? TenantId, string FullName, string Role, bool IsActive, string? Password);
public sealed record RoleAdminDto(Guid Id, Guid? TenantId, string Code, string Name, string Description, bool IsSystem, bool IsActive, List<RolePermissionDto> Permissions);
public sealed record CreateRoleRequest(Guid? TenantId, string Code, string Name, string? Description, List<RolePermissionDto> Permissions);
public sealed record UpdateRoleRequest(string Name, string? Description, bool IsActive, List<RolePermissionDto> Permissions);
public sealed record RolePermissionDto(Guid Id, string Module, string Scenario, bool CanView, bool CanCreate, bool CanEdit, bool CanDelete, bool CanSubmit, bool CanApprove, bool CanOpen, bool CanEvaluate, bool CanAward, bool CanGenerate, bool CanExport, bool CanAudit);
