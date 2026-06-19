using EProcurement.AuthService.Data;
using EProcurement.AuthService.Security;
using EProcurement.Contracts.Auth;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<JwtTokenFactory>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration["Frontend:Url"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("Frontend");

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();
    await AuthDbSeeder.SeedAsync(db, hasher);
}

app.Run();
