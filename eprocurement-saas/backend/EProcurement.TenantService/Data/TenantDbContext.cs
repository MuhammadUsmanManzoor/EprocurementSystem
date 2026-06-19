using EProcurement.TenantService.Models;
using Microsoft.EntityFrameworkCore;

namespace EProcurement.TenantService.Data;

public sealed class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<MasterDataItem> MasterDataItems => Set<MasterDataItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(tenant => tenant.Id);
            entity.HasIndex(tenant => tenant.Slug).IsUnique();
            entity.Property(tenant => tenant.Name).HasMaxLength(160).IsRequired();
            entity.Property(tenant => tenant.Slug).HasMaxLength(80).IsRequired();
        });

        modelBuilder.Entity<MasterDataItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.TenantId, item.Type, item.Code }).IsUnique();
            entity.Property(item => item.Type).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(180).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
        });
    }
}
