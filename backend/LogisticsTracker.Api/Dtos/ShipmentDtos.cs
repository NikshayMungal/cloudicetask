using System.ComponentModel.DataAnnotations;
using LogisticsTracker.Api.Models;

namespace LogisticsTracker.Api.Dtos;

/// <summary>Payload for creating or fully updating a shipment.</summary>
public class ShipmentRequest : IValidatableObject
{
    /// <summary>Optional on create (one is generated if blank). Ignored on update - tracking numbers are immutable.</summary>
    [StringLength(32)]
    public string? TrackingNumber { get; set; }

    [Required(ErrorMessage = "Customer name is required.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "Customer name must be 2-120 characters.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Origin is required.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Origin must be 2-200 characters.")]
    public string Origin { get; set; } = string.Empty;

    [Required(ErrorMessage = "Destination is required.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Destination must be 2-200 characters.")]
    public string Destination { get; set; } = string.Empty;

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;

    [Required(ErrorMessage = "Dispatch date is required.")]
    public DateTime? DispatchDate { get; set; }

    public DateTime? EstimatedDelivery { get; set; }

    [StringLength(80, ErrorMessage = "Driver name must be 80 characters or fewer.")]
    public string? DriverName { get; set; }

    [StringLength(20, ErrorMessage = "Vehicle registration must be 20 characters or fewer.")]
    public string? VehicleRegistration { get; set; }

    [Range(0.1, 100000, ErrorMessage = "Weight must be between 0.1 and 100,000 kg.")]
    public decimal WeightKg { get; set; }

    [StringLength(1000, ErrorMessage = "Notes must be 1000 characters or fewer.")]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DispatchDate.HasValue && EstimatedDelivery.HasValue && EstimatedDelivery < DispatchDate)
        {
            yield return new ValidationResult(
                "Estimated delivery cannot be before the dispatch date.",
                [nameof(EstimatedDelivery)]);
        }
    }
}

/// <summary>Payload for the quick "change status" action on the dispatch board.</summary>
public class StatusUpdateRequest
{
    public ShipmentStatus Status { get; set; }
}

/// <summary>Payload for allocating (or re-allocating) a driver and vehicle.</summary>
public class DriverAssignmentRequest
{
    [Required(ErrorMessage = "Driver name is required.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Driver name must be 2-80 characters.")]
    public string DriverName { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Vehicle registration must be 20 characters or fewer.")]
    public string? VehicleRegistration { get; set; }
}

/// <summary>Shipment as returned to API clients.</summary>
public record ShipmentResponse(
    int Id,
    string TrackingNumber,
    string CustomerName,
    string Origin,
    string Destination,
    ShipmentStatus Status,
    DateTime DispatchDate,
    DateTime? EstimatedDelivery,
    string? DriverName,
    string? VehicleRegistration,
    decimal WeightKg,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ShipmentResponse From(Shipment s) => new(
        s.Id, s.TrackingNumber, s.CustomerName, s.Origin, s.Destination, s.Status,
        s.DispatchDate, s.EstimatedDelivery, s.DriverName, s.VehicleRegistration,
        s.WeightKg, s.Notes, s.CreatedAt, s.UpdatedAt);
}

/// <summary>Aggregates that power the dashboard.</summary>
/// <param name="Total">All shipments.</param>
/// <param name="ByStatus">Count per status (every status is present, even when zero).</param>
/// <param name="Unassigned">Open shipments with no driver allocated.</param>
/// <param name="Overdue">Open shipments whose estimated delivery has passed.</param>
public record ShipmentSummary(int Total, Dictionary<string, int> ByStatus, int Unassigned, int Overdue);
