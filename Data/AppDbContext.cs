using Matibari.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Matibari.Web.Data;

/// <summary>
/// The Entity Framework Core context. Each DbSet is one table; the OnModelCreating method
/// below adds the indexes that keep the shop pages fast once the catalogue grows.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<SiteSetting> Settings => Set<SiteSetting>();

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Name).IsRequired().HasMaxLength(160);
            entity.Property(p => p.Category).IsRequired().HasMaxLength(60);
            entity.Property(p => p.Price).HasPrecision(12, 2);
            entity.Property(p => p.Mrp).HasPrecision(12, 2);
            entity.Property(p => p.Rating).HasPrecision(3, 2);

            // Shop page: filter by category, order by "featured first".
            entity.HasIndex(p => p.Category);
            entity.HasIndex(p => new { p.Active, p.IsCombo });
            entity.HasIndex(p => p.Featured);
        });

        modelBuilder.Entity<SiteSetting>(entity =>
        {
            entity.Property(s => s.BrandName).IsRequired().HasMaxLength(80);
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.Property(a => a.Email).IsRequired().HasMaxLength(160);
            // One account per email address — enforced by the database itself.
            entity.HasIndex(a => a.Email).IsUnique();
        });

        modelBuilder.Entity<OtpChallenge>(entity =>
        {
            entity.Property(o => o.CodeHash).IsRequired();
            // Lookup pattern: "the newest unused challenge for this admin".
            entity.HasIndex(o => new { o.AdminId, o.Used });
            entity.HasIndex(o => o.ExpiresAt);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(o => o.OrderNumber).IsRequired().HasMaxLength(40);
            entity.Property(o => o.Total).HasPrecision(12, 2);
            // Order numbers are quoted to customers, so they must be unique.
            entity.HasIndex(o => o.OrderNumber).IsUnique();
            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => o.CreatedAt);
        });
    }

    /// <summary>
    /// Keeps CreatedAt/UpdatedAt honest without the controllers having to remember them.
    /// Runs on every SaveChanges call.
    /// </summary>
    private void StampTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                switch (entry.Entity)
                {
                    case Product product:
                        // Seed data ships with "made a few weeks ago" dates; only fill in the gaps.
                        if (product.CreatedAt == default) product.CreatedAt = now;
                        if (product.UpdatedAt == default) product.UpdatedAt = now;
                        break;
                    case SiteSetting setting:
                        if (setting.UpdatedAt == default) setting.UpdatedAt = now;
                        break;
                    case AdminUser admin:
                        if (admin.CreatedAt == default) admin.CreatedAt = now;
                        break;
                    case OtpChallenge challenge:
                        if (challenge.CreatedAt == default) challenge.CreatedAt = now;
                        break;
                    case Order order:
                        if (order.CreatedAt == default) order.CreatedAt = now;
                        break;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                switch (entry.Entity)
                {
                    case Product product:
                        product.UpdatedAt = now;
                        break;
                    case SiteSetting setting:
                        setting.UpdatedAt = now;
                        break;
                }
            }
        }
    }

    public override int SaveChanges()
    {
        StampTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }
}
