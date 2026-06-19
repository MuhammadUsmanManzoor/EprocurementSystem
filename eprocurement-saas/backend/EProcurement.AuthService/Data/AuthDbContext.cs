using EProcurement.AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace EProcurement.AuthService.Data;

public sealed class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("auth");

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
            entity.Property(user => user.FullName).HasMaxLength(160).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(64).IsRequired();
            entity.Property(user => user.PasswordHash).IsRequired();
        });
    }
}
