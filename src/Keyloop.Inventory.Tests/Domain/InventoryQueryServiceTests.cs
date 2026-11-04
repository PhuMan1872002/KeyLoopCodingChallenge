using Keyloop.Inventory.Application.Exceptions;
using Keyloop.Inventory.Application.Queries;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;

namespace Keyloop.Inventory.Tests.Domain;

public class InventoryQueryServiceTests
{
    [Fact]
    public void The_current_status_of_a_vehicle_is_its_most_recent_action()
    {
        var vehicle = TestData.Vehicle("VIN-1", actions:
        [
            TestData.Action(ActionStatus.MarketingPromotion, TestData.At(2026, 9, 1)),
            TestData.Action(ActionStatus.PriceReductionPlanned, TestData.At(2026, 10, 15)),
            TestData.Action(ActionStatus.TransferToBranch, TestData.At(2026, 9, 20)),
        ]);

        Assert.Equal(ActionStatus.PriceReductionPlanned, vehicle.CurrentAction?.Status);
    }

    [Fact]
    public async Task Statistics_count_and_value_only_the_aging_stock()
    {
        var service = TestData.Service(
            TestData.Vehicle("VIN-FRESH", daysInStock: 10, listPrice: 10_000m),
            TestData.Vehicle("VIN-BOUNDARY", daysInStock: 90, listPrice: 20_000m),
            TestData.Vehicle("VIN-AGING-1", daysInStock: 91, listPrice: 30_000m),
            TestData.Vehicle("VIN-AGING-2", daysInStock: 200, listPrice: 40_000m));

        var stats = await service.GetStatisticsAsync(TestData.Today);

        Assert.Equal(4, stats.TotalVehicles);
        Assert.Equal(2, stats.AgingVehicles);
        Assert.Equal(70_000m, stats.AgingStockValue);
    }

    [Fact]
    public async Task A_logged_action_is_persisted_and_becomes_the_vehicles_current_status()
    {
        var service = TestData.Service(TestData.Vehicle("VIN-1", daysInStock: 120));

        await service.LogActionAsync("VIN-1", TestData.Action(ActionStatus.WholesaleOrAuction, note: "No interest in 4 months"));

        var vehicles = await service.GetVehiclesAsync(VehicleFilter.None, TestData.Today);
        var current = Assert.Single(vehicles).CurrentAction;

        Assert.Equal(ActionStatus.WholesaleOrAuction, current?.Status);
        Assert.Equal("No interest in 4 months", current?.Note);
    }

    [Fact]
    public async Task Logging_an_action_against_an_unknown_vin_fails_loudly()
    {
        var service = TestData.Service();

        await Assert.ThrowsAsync<VehicleNotFoundException>(() => service.LogActionAsync(
            "VIN-DOES-NOT-EXIST",
            TestData.Action(ActionStatus.WholesaleOrAuction)));
    }

    [Fact]
    public async Task Statistics_separate_aging_stock_from_aging_stock_with_no_plan()
    {
        var planned = TestData.Action(ActionStatus.PriceReductionPlanned);

        var service = TestData.Service(
            TestData.Vehicle("VIN-FRESH", daysInStock: 10),
            TestData.Vehicle("VIN-AGING-PLANNED", daysInStock: 120, actions: [planned]),
            TestData.Vehicle("VIN-AGING-BARE-1", daysInStock: 100),
            TestData.Vehicle("VIN-AGING-BARE-2", daysInStock: 200));

        var stats = await service.GetStatisticsAsync(TestData.Today);

        Assert.Equal(3, stats.AgingVehicles);
        Assert.Equal(2, stats.AgingWithoutPlan);
    }

    [Fact]
    public async Task Statistics_report_the_average_age_and_the_shape_of_the_lot()
    {
        var service = TestData.Service(
            TestData.Vehicle("VIN-A", daysInStock: 10),
            TestData.Vehicle("VIN-B", daysInStock: 50),
            TestData.Vehicle("VIN-C", daysInStock: 80),
            TestData.Vehicle("VIN-D", daysInStock: 180));

        var stats = await service.GetStatisticsAsync(TestData.Today);

        Assert.Equal(80, stats.AverageDaysInStock);
        Assert.Equal(1, stats.CountIn(AgeBucket.UpTo30));
        Assert.Equal(1, stats.CountIn(AgeBucket.From31To60));
        Assert.Equal(1, stats.CountIn(AgeBucket.From61ToThreshold));
        Assert.Equal(1, stats.CountIn(AgeBucket.OverThreshold));

        Assert.Equal(stats.TotalVehicles, stats.BucketCounts.Values.Sum());
    }

    [Fact]
    public async Task Statistics_describe_the_selected_branch_and_not_the_whole_group()
    {
        const string here = "branch-a";
        const string elsewhere = "branch-b";

        var service = TestData.Service(
            TestData.Vehicle("VIN-HERE-1", daysInStock: 120, listPrice: 30_000m, dealershipId: here),
            TestData.Vehicle("VIN-HERE-2", daysInStock: 10, listPrice: 20_000m, dealershipId: here),
            TestData.Vehicle("VIN-ELSE-1", daysInStock: 200, listPrice: 40_000m, dealershipId: elsewhere));

        var group = await service.GetStatisticsAsync(TestData.Today);
        var oneBranch = await service.GetStatisticsAsync(TestData.Today, here);

        Assert.Equal(3, group.TotalVehicles);
        Assert.Equal(2, group.AgingVehicles);

        Assert.Equal(2, oneBranch.TotalVehicles);
        Assert.Equal(1, oneBranch.AgingVehicles);
        Assert.Equal(30_000m, oneBranch.AgingStockValue);
    }

    [Fact]
    public async Task Aging_exposure_is_reported_by_make_in_value_order()
    {
        var service = TestData.Service(
            TestData.Vehicle("VIN-1", make: "Ford", daysInStock: 120, listPrice: 10_000m),
            TestData.Vehicle("VIN-2", make: "Ford", daysInStock: 130, listPrice: 10_000m),
            TestData.Vehicle("VIN-3", make: "Toyota", daysInStock: 140, listPrice: 45_000m),
            TestData.Vehicle("VIN-4", make: "Toyota", daysInStock: 5, listPrice: 90_000m));

        var stats = await service.GetStatisticsAsync(TestData.Today);

        Assert.Equal(
            [("Toyota", 45_000m), ("Ford", 20_000m)],
            stats.AgingByMake.Select(m => (m.Make, m.Value)));
    }

    [Fact]
    public async Task The_spotlight_shows_the_oldest_unattended_vehicles_first()
    {
        var planned = TestData.Action(ActionStatus.WholesaleOrAuction);

        var service = TestData.Service(
            TestData.Vehicle("VIN-FRESH", daysInStock: 10),
            TestData.Vehicle("VIN-OLDEST-BUT-PLANNED", daysInStock: 300, actions: [planned]),
            TestData.Vehicle("VIN-SECOND", daysInStock: 120),
            TestData.Vehicle("VIN-FIRST", daysInStock: 180));

        var spotlight = await service.GetSpotlightAsync(TestData.Today, count: 5);

        Assert.Equal(["VIN-FIRST", "VIN-SECOND"], spotlight.Select(v => v.Vin));
    }

    [Fact]
    public async Task Statistics_over_an_empty_inventory_do_not_divide_by_zero()
    {
        var service = TestData.Service();

        var stats = await service.GetStatisticsAsync(TestData.Today);

        Assert.Equal(0, stats.TotalVehicles);
        Assert.Equal(0, stats.AverageDaysInStock);
        Assert.Equal(0, stats.AgingShare);
    }
}
