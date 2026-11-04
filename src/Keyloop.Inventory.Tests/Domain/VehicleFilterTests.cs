using Keyloop.Inventory.Application.Queries;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;

namespace Keyloop.Inventory.Tests.Domain;

public class VehicleFilterTests
{
    private static readonly Vehicle FreshFord = TestData.Vehicle("VIN-FRESH-FORD", "Ford", "Focus", daysInStock: 10);
    private static readonly Vehicle AgingFord = TestData.Vehicle("VIN-AGING-FORD", "Ford", "Kuga", daysInStock: 120);
    private static readonly Vehicle AgingBmw = TestData.Vehicle("VIN-AGING-BMW", "BMW", "320i", daysInStock: 200);

    private static readonly Vehicle[] Inventory = [FreshFord, AgingFord, AgingBmw];

    [Fact]
    public void An_empty_filter_returns_every_vehicle()
    {
        var result = VehicleFilter.None.Apply(Inventory, TestData.Today);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void A_single_filter_narrows_to_matching_vehicles()
    {
        var filter = new VehicleFilter { Make = "Ford" };

        var result = filter.Apply(Inventory, TestData.Today);

        Assert.Equal([AgingFord, FreshFord], result);
    }

    [Fact]
    public void Filters_compose_so_each_one_narrows_the_last()
    {
        var filter = new VehicleFilter { Make = "Ford", AgingOnly = true };

        var result = filter.Apply(Inventory, TestData.Today);

        Assert.Equal([AgingFord], result);
    }

    [Fact]
    public void A_filter_that_matches_nothing_returns_an_empty_result()
    {
        var filter = new VehicleFilter { Make = "Ford", Model = "320i" };

        var result = filter.Apply(Inventory, TestData.Today);

        Assert.Empty(result);
    }

    [Fact]
    public void Search_matches_on_a_partial_case_insensitive_fragment()
    {
        var filter = new VehicleFilter { Search = "aging-bmw" };

        var result = filter.Apply(Inventory, TestData.Today);

        Assert.Equal([AgingBmw], result);
    }

    [Theory]
    [InlineData(0, AgeBand.Fresh)]
    [InlineData(75, AgeBand.Fresh)]
    [InlineData(76, AgeBand.DueSoon)]
    [InlineData(90, AgeBand.DueSoon)]
    [InlineData(91, AgeBand.Aging)]
    [InlineData(150, AgeBand.Aging)]
    [InlineData(151, AgeBand.Critical)]
    public void Each_band_boundary_falls_on_the_documented_side(int daysInStock, AgeBand expected)
    {
        var stockedOn = TestData.Today.AddDays(-daysInStock);

        Assert.Equal(expected, AgeBands.Of(stockedOn, TestData.Today));
    }

    [Fact]
    public void Critical_stock_is_still_aging_stock()
    {
        var band = AgeBands.Of(TestData.Today.AddDays(-200), TestData.Today);

        Assert.Equal(AgeBand.Critical, band);
        Assert.True(AgingPolicy.IsAging(TestData.Today.AddDays(-200), TestData.Today));
        Assert.Equal([AgingBmw, AgingFord], new VehicleFilter { AgingOnly = true }.Apply(Inventory, TestData.Today));
    }

    [Fact]
    public void Needs_action_only_excludes_aging_vehicles_that_already_have_a_decision()
    {
        var plannedFord = AgingFord with
        {
            Actions = [TestData.Action(ActionStatus.PriceReductionPlanned)],
        };

        var filter = new VehicleFilter { NeedsActionOnly = true };

        var result = filter.Apply([FreshFord, plannedFord, AgingBmw], TestData.Today);

        Assert.Equal([AgingBmw], result);
    }

    [Theory]
    [InlineData(SortOrder.OldestFirst, "VIN-AGING-BMW", "VIN-AGING-FORD", "VIN-FRESH-FORD")]
    [InlineData(SortOrder.NewestFirst, "VIN-FRESH-FORD", "VIN-AGING-FORD", "VIN-AGING-BMW")]
    [InlineData(SortOrder.PriceHighToLow, "VIN-AGING-BMW", "VIN-AGING-FORD", "VIN-FRESH-FORD")]
    public void Sort_order_is_applied_after_filtering(SortOrder sort, params string[] expected)
    {
        var priced = new[]
        {
            FreshFord with { ListPrice = 10_000m },
            AgingFord with { ListPrice = 20_000m },
            AgingBmw with { ListPrice = 30_000m },
        };

        var result = (VehicleFilter.None with { Sort = sort }).Apply(priced, TestData.Today);

        Assert.Equal(expected, result.Select(v => v.Vin));
    }
}
