using Microsoft.EntityFrameworkCore;
using RentEase.Models;
using RentEase.Services;

namespace RentEase.Data;

public class RentEaseDbContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;

    public RentEaseDbContext(
        DbContextOptions<RentEaseDbContext> options,
        ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<BookingRequest> BookingRequests => Set<BookingRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        modelBuilder.Entity<Property>()
            .HasQueryFilter(p => _tenantProvider.CurrentTenantId == null || p.TenantId == _tenantProvider.CurrentTenantId);

        modelBuilder.Entity<BookingRequest>()
            .HasQueryFilter(b => _tenantProvider.CurrentTenantId == null || b.TenantId == _tenantProvider.CurrentTenantId);

        modelBuilder.Entity<User>()
            .HasQueryFilter(u => _tenantProvider.CurrentTenantId == null || u.TenantId == _tenantProvider.CurrentTenantId);

        modelBuilder.Entity<Property>()
            .HasOne(p => p.Tenant)
            .WithMany(t => t.Properties)
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Property>()
            .HasOne(p => p.Owner)
            .WithMany(u => u.Properties)
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PropertyImage>()
            .HasOne(pi => pi.Property)
            .WithMany(p => p.Images)
            .HasForeignKey(pi => pi.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Property>()
            .HasMany(p => p.Amenities)
            .WithMany(a => a.Properties);

        modelBuilder.Entity<BookingRequest>()
            .HasOne(b => b.Property)
            .WithMany(p => p.Bookings)
            .HasForeignKey(b => b.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BookingRequest>()
            .HasOne(b => b.Client)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}