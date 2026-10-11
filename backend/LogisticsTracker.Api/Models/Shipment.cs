using System.Text.Json.Serialization;

namespace LogisticsTracker.Api.Models;

/// <summary>Lifecycle of a shipment, from booking to hand-over.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShipmentStatus
{
    Pending,
    InTransit,
    OutForDelivery,
    Delivered,
    Delayed,
    Cancelled
}

/// <summary>Database entity for a single freight consignment.</summary>
public class Shipment
{
    public int Id { get; set; }

    /// <summary>Unique, human-friendly reference, e.g. LT-20250114-48213.</summary>
    public string TrackingNumber { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;

    /// <summary>All timestamps are stored in UTC.</summary>
    public DateTime DispatchDate { get; set; }
    public DateTime? EstimatedDelivery { get; set; }

    /// <summary>Allocated driver. Null/empty means the shipment still needs a driver.</summary>
    public string? DriverName { get; set; }
    public string? VehicleRegistration { get; set; }

    public decimal WeightKg { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
