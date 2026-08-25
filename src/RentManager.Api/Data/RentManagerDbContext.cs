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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shop>()
            .HasOne(s => s.Tenant)
            .WithOne(t => t.Shop)
            .HasForeignKey<Tenant>(t => t.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

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

        modelBuilder.Entity<Rent>()
            .HasIndex(r => new { r.TenantId, r.Year, r.Month })
            .IsUnique();
    }
}
