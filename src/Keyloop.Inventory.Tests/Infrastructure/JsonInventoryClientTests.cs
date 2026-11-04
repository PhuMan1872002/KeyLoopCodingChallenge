using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Keyloop.Inventory.Application.Exceptions;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;
using Keyloop.Inventory.Infrastructure.Persistence;

namespace Keyloop.Inventory.Tests.Infrastructure;

public sealed class JsonInventoryClientTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "keyloop-inventory-tests-" + Guid.NewGuid().ToString("N"));

    private InventoryStoreOptions NewOptions(int seedCount = 25) => new()
    {
        FilePath = Path.Combine(_directory, "inventory.json"),
        SeedVehicleCount = seedCount,
    };

    private static JsonInventoryClient Create(InventoryStoreOptions options) =>
        new(options, new StubEnvironment(), NullLogger<JsonInventoryClient>.Instance);

    [Fact]
    public async Task An_action_survives_the_process_that_logged_it()
    {
        var options = NewOptions();
        var recordedAt = new DateTimeOffset(2026, 11, 2, 9, 30, 0, TimeSpan.Zero);
        string vin;

        using (var client = Create(options))
        {
            var vehicles = await client.GetAllAsync();
            Assert.Equal(25, vehicles.Count);

            vin = vehicles[0].Vin;
            await client.AppendActionAsync(vin, new VehicleAction
            {
                Status = ActionStatus.WholesaleOrAuction,
                Note = "No interest in 4 months",
                RecordedAt = recordedAt,
            });
        }

        using var reopened = Create(options);
        var reloaded = await reopened.GetAllAsync();
        var vehicle = Assert.Single(reloaded, v => v.Vin == vin);

        Assert.Equal(ActionStatus.WholesaleOrAuction, vehicle.CurrentAction?.Status);
        Assert.Equal("No interest in 4 months", vehicle.CurrentAction?.Note);
        Assert.Equal(recordedAt, vehicle.CurrentAction?.RecordedAt);
    }

    [Fact]
    public async Task Concurrent_writes_do_not_lose_actions()
    {
        var options = NewOptions(seedCount: 5);
        using var client = Create(options);

        var vin = (await client.GetAllAsync())[0].Vin;

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => client.AppendActionAsync(vin, new VehicleAction
        {
            Status = ActionStatus.PriceReductionPlanned,
            Note = $"concurrent-{i}",
            RecordedAt = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero).AddSeconds(i),
        })));

        var vehicle = Assert.Single(await client.GetAllAsync(), v => v.Vin == vin);
        Assert.Equal(20, vehicle.Actions.Count);
    }

    [Fact]
    public async Task Logging_against_a_vin_that_is_not_in_stock_is_rejected()
    {
        using var client = Create(NewOptions(seedCount: 5));

        await Assert.ThrowsAsync<VehicleNotFoundException>(() => client.AppendActionAsync(
            "NOT-A-REAL-VIN",
            new VehicleAction { Status = ActionStatus.WholesaleOrAuction, RecordedAt = DateTimeOffset.UnixEpoch }));
    }

    [Fact]
    public async Task A_store_written_by_an_older_version_is_regenerated_rather_than_fatal()
    {
        var options = NewOptions(seedCount: 5);
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(options.FilePath, """[{"vin":"OLD-SHAPE-VIN"}]""");

        using var client = Create(options);

        var vehicles = await client.GetAllAsync();

        Assert.Equal(5, vehicles.Count);
        Assert.DoesNotContain(vehicles, v => v.Vin == "OLD-SHAPE-VIN");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Keyloop.Inventory.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
