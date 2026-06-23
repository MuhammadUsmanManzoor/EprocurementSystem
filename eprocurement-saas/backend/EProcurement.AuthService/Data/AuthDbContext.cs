using EProcurement.AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace EProcurement.AuthService.Data;

public sealed class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("auth");

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => user.Username).IsUnique();
            entity.Property(user => user.Username).HasMaxLength(80).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
            entity.Property(user => user.FullName).HasMaxLength(160).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(64).IsRequired();
            entity.Property(user => user.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<AppRole>(entity =>
        {
            entity.HasKey(role => role.Id);
            entity.HasIndex(role => new { role.TenantId, role.Code }).IsUnique();
            entity.Property(role => role.Code).HasMaxLength(64).IsRequired();
            entity.Property(role => role.Name).HasMaxLength(160).IsRequired();
            entity.Property(role => role.Description).HasMaxLength(500);
            entity.HasMany(role => role.Permissions).WithOne().HasForeignKey(permission => permission.RoleId);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(permission => permission.Id);
            entity.HasIndex(permission => new { permission.RoleId, permission.Module, permission.Scenario }).IsUnique();
            entity.Property(permission => permission.Module).HasMaxLength(120).IsRequired();
            entity.Property(permission => permission.Scenario).HasMaxLength(180).IsRequired();
        });
    }
}
