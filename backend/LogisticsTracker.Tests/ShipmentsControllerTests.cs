using LogisticsTracker.Api.Controllers;
using LogisticsTracker.Api.Data;
using LogisticsTracker.Api.Dtos;
using LogisticsTracker.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogisticsTracker.Tests;

/// <summary>Unit tests run against the EF Core in-memory provider, so no database is needed in CI.</summary>
public class ShipmentsControllerTests
{
    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ShipmentRequest NewRequest(string? tracking = null, string customer = "Acme Freight",
        ShipmentStatus status = ShipmentStatus.Pending, string? driver = null) => new()
    {
        TrackingNumber = tracking,
        CustomerName = customer,
        Origin = "Durban",
        Destination = "Johannesburg",
        Status = status,
        DispatchDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc),
        EstimatedDelivery = new DateTime(2025, 1, 12, 0, 0, 0, DateTimeKind.Utc),
        DriverName = driver,
        WeightKg = 500
    };

    [Fact]
    public async Task Create_GeneratesTrackingNumber_AndReturns201()
    {
        using var db = NewDb();
        var controller = new ShipmentsController(db);

        var result = await controller.Create(NewRequest(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var body = Assert.IsType<ShipmentResponse>(created.Value);
        Assert.StartsWith("LT-", body.TrackingNumber);
        Assert.Equal(1, await db.Shipments.CountAsync());
    }

    [Fact]
    public async Task Create_DuplicateTrackingNumber_ReturnsConflict()
    {
        using var db = NewDb();
        var controller = new ShipmentsController(db);
        await controller.Create(NewRequest("lt-dup-1"), CancellationToken.None);

        var result = await controller.Create(NewRequest("LT-DUP-1"), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetAll_FiltersByStatusAndSearch()
    {
        using var db = NewDb();
        var controller = new ShipmentsController(db);
        await controller.Create(NewRequest("LT-A", "Coastal Hardware", ShipmentStatus.InTransit, "Sipho"), CancellationToken.None);
        await controller.Create(NewRequest("LT-B", "Midlands Produce", ShipmentStatus.Pending), CancellationToken.None);

        var byStatus = Assert.IsType<OkObjectResult>(
            (await controller.GetAll(null, ShipmentStatus.InTransit, false, CancellationToken.None)).Result);
        Assert.Single(Assert.IsAssignableFrom<IEnumerable<ShipmentResponse>>(byStatus.Value));

        var bySearch = Assert.IsType<OkObjectResult>(
            (await controller.GetAll("midlands", null, false, CancellationToken.None)).Result);
        var match = Assert.Single(Assert.IsAssignableFrom<IEnumerable<ShipmentResponse>>(bySearch.Value));
        Assert.Equal("LT-B", match.TrackingNumber);
    }

    [Fact]
    public async Task GetAll_Unassigned_ReturnsOnlyOpenShipmentsWithoutDriver()
    {
        using var db = NewDb();
        var controller = new ShipmentsController(db);
        await controller.Create(NewRequest("LT-1", driver: "Sipho"), CancellationToken.None);
        await controller.Create(NewRequest("LT-2"), CancellationToken.None);
        await controller.Create(NewRequest("LT-3", status: ShipmentStatus.Delivered), CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(
            (await controller.GetAll(null, null, true, CancellationToken.None)).Result);

        var match = Assert.Single(Assert.IsAssignableFrom<IEnumerable<ShipmentResponse>>(result.Value));
        Assert.Equal("LT-2", match.TrackingNumber);
    }

    [Fact]
    public async Task Update_MissingShipment_ReturnsNotFound()
    {
        using var db = NewDb();
        var controller = new ShipmentsController(db);

        var result = await controller.Update(999, NewRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ChangesFields_ButKeepsTrackingNumber()
    {
        using var db = NewDb();
        var controller = new ShipmentsController(db);
        await controller.Create(NewRequest("LT-KEEP"), CancellationToken.None);
        var id = (await db.Shipments.SingleAsync()).Id;

        var update = NewRequest("LT-CHANGED", "New Customer", ShipmentStatus.Delayed, "Thandi");
        var result = Assert.IsType<OkObjectResult>((await controller.Update(id, update, CancellationToken.None)).Result);

        var body = Assert.IsType<ShipmentResponse>(result.Value);
        Assert.Equal("LT-KEEP", body.TrackingNumber);
        Assert.Equal("New Customer", body.CustomerName);
        Assert.Equal(ShipmentStatus.Delayed, body.Status);
        Assert.Equal("Thandi", body.DriverName);
    }

    [Fact]
    public async Task UpdateStatus_And_AssignDriver_PersistChanges()
    {
        using var db = NewDb();
        var controller = new ShipmentsController(db);
        await controller.Create(NewRequest("LT-S"), CancellationToken.None);
        var id = (await db.Shipments.SingleAsync()).Id;

        await controller.UpdateStatus(id, new StatusUpdateRequest { Status = ShipmentStatus.OutForDelivery }, CancellationToken.None);
        await controller.AssignDriver(id, new DriverAssignmentRequest { DriverName = "Lerato", VehicleRegistration = "GP 1" }, CancellationToken.None);

        var saved = await db.Shipments.SingleAsync();
        Assert.Equal(ShipmentStatus.OutForDelivery, saved.Status);
        Assert.Equal("Lerato", saved.DriverName);
        Assert.Equal("GP 1", saved.VehicleRegistration);
    }

    [Fact]
    public async Task Delete_RemovesShipment()
    {
        using var db = NewDb();
        var controller = new ShipmentsController(db);
        await controller.Create(NewRequest("LT-DEL"), CancellationToken.None);
        var id = (await db.Shipments.SingleAsync()).Id;

        var result = await controller.Delete(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.Shipments);
    }

    [Fact]
    public async Task Summary_CountsStatusesUnassignedAndOverdue()
    {
        using var db = NewDb();
        var controller = new ShipmentsController(db);
        // Estimated delivery in 2025 is in the past, so every open shipment counts as overdue.
        await controller.Create(NewRequest("LT-1", status: ShipmentStatus.InTransit, driver: "Sipho"), CancellationToken.None);
        await controller.Create(NewRequest("LT-2", status: ShipmentStatus.Pending), CancellationToken.None);
        await controller.Create(NewRequest("LT-3", status: ShipmentStatus.Delivered), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>((await controller.GetSummary(CancellationToken.None)).Result);
        var summary = Assert.IsType<ShipmentSummary>(ok.Value);

        Assert.Equal(3, summary.Total);
        Assert.Equal(1, summary.ByStatus["InTransit"]);
        Assert.Equal(0, summary.ByStatus["Cancelled"]);
        Assert.Equal(1, summary.Unassigned);
        Assert.Equal(2, summary.Overdue);
    }

    [Theory]
    [InlineData("postgres://user:p%40ss@ep-cool-123.neon.tech/logistics?sslmode=require", "ep-cool-123.neon.tech", "p@ss")]
    [InlineData("postgresql://user:secret@db:5432/logistics", "db", "secret")]
    public void ConnectionStringResolver_ParsesPostgresUris(string uri, string expectedHost, string expectedPassword)
    {
        var result = ConnectionStringResolver.FromUri(uri);

        Assert.Contains($"Host={expectedHost}", result);
        Assert.Contains($"Password={expectedPassword}", result);
        Assert.Contains("Database=logistics", result);
    }
}
