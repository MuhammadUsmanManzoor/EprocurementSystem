using System.Text;
using EProcurement.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
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
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "sap-integration-service" }));

app.MapPost("/api/sap/export-purchase-order", (SapExportPurchaseOrderRequest request, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (user.Role != "Finance" && !user.IsTenantAdmin && !user.IsSuperAdmin) return Results.Forbid();
    if (!user.CanAccessTenant(request.TenantId)) return Results.Forbid();
    return Results.Accepted($"/api/sap/jobs/{Guid.NewGuid()}", new
    {
        Status = "QueuedPlaceholder",
        Message = "SAP Business One integration is prepared as a placeholder for a future connector.",
        request.TenantId,
        request.PurchaseOrderId
    });
}).RequireAuthorization();

app.Run();

public sealed record SapExportPurchaseOrderRequest(Guid TenantId, Guid PurchaseOrderId);
