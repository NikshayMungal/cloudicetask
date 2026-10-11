using LogisticsTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LogisticsTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Shipment> Shipments => Set<Shipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shipment>(e =>
        {
            e.Property(s => s.TrackingNumber).IsRequired().HasMaxLength(32);
            e.HasIndex(s => s.TrackingNumber).IsUnique();

            e.Property(s => s.CustomerName).IsRequired().HasMaxLength(120);
            e.Property(s => s.Origin).IsRequired().HasMaxLength(200);
            e.Property(s => s.Destination).IsRequired().HasMaxLength(200);
            e.Property(s => s.DriverName).HasMaxLength(80);
            e.Property(s => s.VehicleRegistration).HasMaxLength(20);
            e.Property(s => s.Notes).HasMaxLength(1000);
            e.Property(s => s.WeightKg).HasPrecision(10, 2);

            // Store the enum as readable text ("InTransit") rather than an opaque integer.
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(s => s.Status);
        });
    }
}
