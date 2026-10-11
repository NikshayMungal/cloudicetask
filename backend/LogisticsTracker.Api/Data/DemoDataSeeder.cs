using LogisticsTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LogisticsTracker.Api.Data;

/// <summary>Inserts a handful of realistic shipments so the dashboard is not empty during a demo.</summary>
public static class DemoDataSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Shipments.AnyAsync()) return; // never overwrite real data

        var today = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;

        Shipment Make(string tracking, string customer, string from, string to, ShipmentStatus status,
                      int dispatchDaysAgo, int etaInDays, string? driver, string? reg, decimal kg, string? notes = null) => new()
        {
            TrackingNumber = tracking,
            CustomerName = customer,
            Origin = from,
            Destination = to,
            Status = status,
            DispatchDate = today.AddDays(-dispatchDaysAgo),
            EstimatedDelivery = today.AddDays(etaInDays),
            DriverName = driver,
            VehicleRegistration = reg,
            WeightKg = kg,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Shipments.AddRange(
            Make("LT-DEMO-1001", "Coastal Hardware", "Durban", "Johannesburg", ShipmentStatus.InTransit, 1, 1, "Sipho Dlamini", "ND 482-190", 3200),
            Make("LT-DEMO-1002", "Midlands Fresh Produce", "Pietermaritzburg", "Durban", ShipmentStatus.OutForDelivery, 0, 0, "Thandi Naidoo", "NP 118-554", 850),
            Make("LT-DEMO-1003", "Karoo Medical Supplies", "Port Elizabeth", "Bloemfontein", ShipmentStatus.Delayed, 4, -1, "Pieter van Wyk", "EC 903-221", 420, "Road closure on N10, rerouting."),
            Make("LT-DEMO-1004", "Zululand Timber", "Richards Bay", "Durban", ShipmentStatus.Pending, 0, 2, null, null, 12500, "Awaiting flatbed allocation."),
            Make("LT-DEMO-1005", "Highveld Electronics", "Johannesburg", "Cape Town", ShipmentStatus.Delivered, 5, -2, "Lerato Mokoena", "GP 337-880", 1900),
            Make("LT-DEMO-1006", "Bayside Cold Storage", "Durban", "Pietermaritzburg", ShipmentStatus.Pending, 0, 1, null, null, 2100),
            Make("LT-DEMO-1007", "Ballito Builders Merchant", "Durban", "Ballito", ShipmentStatus.Cancelled, 2, 0, null, null, 640, "Customer cancelled order."),
            Make("LT-DEMO-1008", "Drakensberg Outfitters", "Ladysmith", "Johannesburg", ShipmentStatus.InTransit, 2, 0, "Sipho Dlamini", "ND 482-190", 760));

        await db.SaveChangesAsync();
    }
}
