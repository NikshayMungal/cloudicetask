using LogisticsTracker.Api.Data;
using LogisticsTracker.Api.Dtos;
using LogisticsTracker.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogisticsTracker.Api.Controllers;

/// <summary>Create, track, allocate and close out freight shipments.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ShipmentsController(AppDbContext db) : ControllerBase
{
    /// <summary>Statuses that mean the shipment is finished and no longer needs dispatcher attention.</summary>
    private static readonly ShipmentStatus[] ClosedStatuses = [ShipmentStatus.Delivered, ShipmentStatus.Cancelled];

    /// <summary>List shipments, newest dispatch first, with optional filters.</summary>
    /// <param name="search">Matches tracking number, customer, origin, destination or driver (case-insensitive).</param>
    /// <param name="status">Only return shipments in this status.</param>
    /// <param name="unassigned">When true, only open shipments that have no driver.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ShipmentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ShipmentResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] ShipmentStatus? status,
        [FromQuery] bool unassigned = false,
        CancellationToken ct = default)
    {
        var query = db.Shipments.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s =>
                s.TrackingNumber.ToLower().Contains(term) ||
                s.CustomerName.ToLower().Contains(term) ||
                s.Origin.ToLower().Contains(term) ||
                s.Destination.ToLower().Contains(term) ||
                (s.DriverName != null && s.DriverName.ToLower().Contains(term)));
        }

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        if (unassigned)
            query = query.Where(s => !ClosedStatuses.Contains(s.Status) && (s.DriverName == null || s.DriverName == ""));

        var items = await query
            .OrderByDescending(s => s.DispatchDate)
            .ThenByDescending(s => s.Id)
            .ToListAsync(ct);

        return Ok(items.Select(ShipmentResponse.From));
    }

    /// <summary>Dashboard totals: counts per status, unassigned and overdue shipments.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ShipmentSummary), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShipmentSummary>> GetSummary(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var counts = await db.Shipments.AsNoTracking()
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var unassigned = await db.Shipments.CountAsync(s =>
            !ClosedStatuses.Contains(s.Status) && (s.DriverName == null || s.DriverName == ""), ct);

        var overdue = await db.Shipments.CountAsync(s =>
            !ClosedStatuses.Contains(s.Status) && s.EstimatedDelivery != null && s.EstimatedDelivery < now, ct);

        var byStatus = Enum.GetValues<ShipmentStatus>().ToDictionary(
            st => st.ToString(),
            st => counts.FirstOrDefault(c => c.Status == st)?.Count ?? 0);

        return Ok(new ShipmentSummary(byStatus.Values.Sum(), byStatus, unassigned, overdue));
    }

    /// <summary>Get one shipment by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ShipmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentResponse>> GetById(int id, CancellationToken ct)
    {
        var shipment = await db.Shipments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return shipment is null ? NotFound() : Ok(ShipmentResponse.From(shipment));
    }

    /// <summary>Create a shipment. A tracking number is generated when none is supplied.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ShipmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShipmentResponse>> Create([FromBody] ShipmentRequest request, CancellationToken ct)
    {
        var tracking = string.IsNullOrWhiteSpace(request.TrackingNumber)
            ? await GenerateTrackingNumberAsync(ct)
            : request.TrackingNumber.Trim().ToUpperInvariant();

        if (await db.Shipments.AnyAsync(s => s.TrackingNumber == tracking, ct))
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Duplicate tracking number",
                Detail = $"A shipment with tracking number '{tracking}' already exists."
            });

        var now = DateTime.UtcNow;
        var shipment = new Shipment { TrackingNumber = tracking, CreatedAt = now };
        Apply(shipment, request, now);

        db.Shipments.Add(shipment);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = shipment.Id }, ShipmentResponse.From(shipment));
    }

    /// <summary>Replace the editable fields of a shipment. The tracking number never changes.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ShipmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentResponse>> Update(int id, [FromBody] ShipmentRequest request, CancellationToken ct)
    {
        var shipment = await db.Shipments.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (shipment is null) return NotFound();

        Apply(shipment, request, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);

        return Ok(ShipmentResponse.From(shipment));
    }

    /// <summary>Quickly change just the status (used by the dropdown on the dispatch board).</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(ShipmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentResponse>> UpdateStatus(int id, [FromBody] StatusUpdateRequest request, CancellationToken ct)
    {
        var shipment = await db.Shipments.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (shipment is null) return NotFound();

        shipment.Status = request.Status;
        shipment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(ShipmentResponse.From(shipment));
    }

    /// <summary>Allocate or re-allocate a driver (and optionally a vehicle) to a shipment.</summary>
    [HttpPatch("{id:int}/driver")]
    [ProducesResponseType(typeof(ShipmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentResponse>> AssignDriver(int id, [FromBody] DriverAssignmentRequest request, CancellationToken ct)
    {
        var shipment = await db.Shipments.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (shipment is null) return NotFound();

        shipment.DriverName = request.DriverName.Trim();
        shipment.VehicleRegistration = NullIfBlank(request.VehicleRegistration);
        shipment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(ShipmentResponse.From(shipment));
    }

    /// <summary>Permanently delete a shipment.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var shipment = await db.Shipments.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (shipment is null) return NotFound();

        db.Shipments.Remove(shipment);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---------- helpers ----------

    private static void Apply(Shipment shipment, ShipmentRequest r, DateTime now)
    {
        shipment.CustomerName = r.CustomerName.Trim();
        shipment.Origin = r.Origin.Trim();
        shipment.Destination = r.Destination.Trim();
        shipment.Status = r.Status;
        shipment.DispatchDate = ToUtc(r.DispatchDate!.Value);
        shipment.EstimatedDelivery = r.EstimatedDelivery.HasValue ? ToUtc(r.EstimatedDelivery.Value) : null;
        shipment.DriverName = NullIfBlank(r.DriverName);
        shipment.VehicleRegistration = NullIfBlank(r.VehicleRegistration);
        shipment.WeightKg = r.WeightKg;
        shipment.Notes = NullIfBlank(r.Notes);
        shipment.UpdatedAt = now;
    }

    /// <summary>PostgreSQL 'timestamptz' only accepts UTC values, so normalise whatever the client sent.</summary>
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<string> GenerateTrackingNumberAsync(CancellationToken ct)
    {
        while (true)
        {
            var candidate = $"LT-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(10000, 99999)}";
            if (!await db.Shipments.AnyAsync(s => s.TrackingNumber == candidate, ct))
                return candidate;
        }
    }
}
