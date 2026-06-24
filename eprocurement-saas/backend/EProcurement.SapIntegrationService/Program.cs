using System.Text;
using EProcurement.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
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

app.MapGet("/api/sap/customers", async (int? take, IConfiguration configuration, HttpContext http) =>
{
    var user = new CurrentUser(http.User);
    if (!user.IsSuperAdmin && !user.IsTenantAdmin && user.Role != "Procurement" && user.Role != "Finance" && user.Role != "Auditor")
    {
        return Results.Forbid();
    }

    var connectionString = configuration.GetConnectionString("SapCompany");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Problem("SAP SQL connection string is not configured. Set ConnectionStrings__SapCompany.");
    }

    var limit = Math.Clamp(take ?? 5, 1, 100);
    var customers = new List<SapCustomerDto>();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT TOP (@Take)
            CardCode,
            CardName,
            Phone1,
            E_Mail
        FROM dbo.OCRD
        WHERE CardType = 'C'
        ORDER BY CardCode;
        """;
    command.Parameters.AddWithValue("@Take", limit);

    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        customers.Add(new SapCustomerDto(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3)));
    }

    return Results.Ok(customers);
}).RequireAuthorization();

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
public sealed record SapCustomerDto(string CardCode, string CardName, string? Phone, string? Email);
