using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;
using Keyloop.Inventory.Infrastructure.Persistence;

namespace Keyloop.Inventory.Tests.Infrastructure;

public class InventorySeedTests
{
    private static readonly DateOnly Today = new(2026, 11, 2);

    private static readonly int ShippedCount = new InventoryStoreOptions().SeedVehicleCount;

    [Fact]
    public void The_same_seed_produces_the_same_inventory_on_every_run()
    {
        var first = InventorySeed.Generate(ShippedCount, Today);
        var second = InventorySeed.Generate(ShippedCount, Today);

        Assert.Equal(
            first.Select(v => (v.Vin, v.Make, v.Model, v.ListPrice, v.StockedOn)),
            second.Select(v => (v.Vin, v.Make, v.Model, v.ListPrice, v.StockedOn)));
    }

    [Fact]
    public void Aging_stock_is_a_minority_of_the_lot()
    {
        var vehicles = InventorySeed.Generate(ShippedCount, Today);

        var agingShare = vehicles.Count(v => AgingPolicy.IsAging(v.StockedOn, Today)) / (double)vehicles.Count;

        Assert.InRange(agingShare, 0.10, 0.35);
    }

    [Fact]
    public void Every_age_band_is_represented()
    {
        var vehicles = InventorySeed.Generate(ShippedCount, Today);

        var bands = vehicles.Select(v => AgeBands.Of(v.StockedOn, Today)).ToHashSet();

        Assert.Equal(
            [AgeBand.Fresh, AgeBand.DueSoon, AgeBand.Aging, AgeBand.Critical],
            bands.Order());
    }

    [Fact]
    public void The_lot_opens_with_some_aging_stock_reviewed_and_some_not()
    {
        var vehicles = InventorySeed.Generate(ShippedCount, Today);
        var aging = vehicles.Where(v => AgingPolicy.IsAging(v.StockedOn, Today)).ToList();

        Assert.Contains(aging, v => v.CurrentAction is not null);
        Assert.Contains(aging, v => v.CurrentAction is null);
    }

    [Fact]
    public void Vehicles_inside_the_threshold_carry_no_logged_decision()
    {
        var vehicles = InventorySeed.Generate(ShippedCount, Today);

        Assert.All(
            vehicles.Where(v => !AgingPolicy.IsAging(v.StockedOn, Today)),
            v => Assert.Empty(v.Actions));
    }

    [Fact]
    public void A_price_reduction_carries_the_price_it_proposes()
    {
        var vehicles = InventorySeed.Generate(ShippedCount, Today);

        var actions = vehicles.SelectMany(v => v.Actions).ToList();

        Assert.All(
            actions.Where(a => a.Status == ActionStatus.PriceReductionPlanned),
            a => Assert.NotNull(a.ProposedPrice));

        Assert.All(
            actions.Where(a => a.Status != ActionStatus.PriceReductionPlanned),
            a => Assert.Null(a.ProposedPrice));
    }

    [Fact]
    public void Generated_vins_are_seventeen_characters_and_avoid_the_forbidden_letters()
    {
        var vehicles = InventorySeed.Generate(ShippedCount, Today);

        Assert.All(vehicles, v =>
        {
            Assert.Equal(17, v.Vin.Length);
            Assert.DoesNotContain(v.Vin, c => c is 'I' or 'O' or 'Q');
        });
    }
}
