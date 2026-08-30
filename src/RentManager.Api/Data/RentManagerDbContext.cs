using Microsoft.EntityFrameworkCore;
using RentManager.Api.Models;

namespace RentManager.Api.Data;

public class RentManagerDbContext : DbContext
{
    public RentManagerDbContext(DbContextOptions<RentManagerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Rent> Rents => Set<Rent>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PushSubscription>()
            .HasIndex(s => s.Endpoint)
            .IsUnique();

        modelBuilder.Entity<Shop>()
            .HasOne(s => s.Tenant)
            .WithOne(t => t.Shop)
            .HasForeignKey<Tenant>(t => t.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        // Only active shop names need to be unique. A deleted shop keeps
        // its row for history but stops blocking the name.
        modelBuilder.Entity<Shop>()
            .HasIndex(s => s.Name)
            .IsUnique()
            .HasFilter("\"IsActive\" = true");

        modelBuilder.Entity<Tenant>()
            .Property(t => t.MonthlyRent)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Tenant>()
            .Property(t => t.SecurityDeposit)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Rent>()
            .Property(r => r.AmountDue)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Rent>()
            .Property(r => r.AmountPaid)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Rent)
            .WithMany(r => r.Payments)
            .HasForeignKey(p => p.RentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Tenant)
            .WithMany(t => t.Notifications)
            .HasForeignKey(n => n.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Rent)
            .WithMany()
            .HasForeignKey(n => n.RentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Rent>()
            .HasIndex(r => new { r.TenantId, r.Year, r.Month })
            .IsUnique();

        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.Username)
            .IsUnique();
    }
}
