using Keyloop.Inventory.Application.Dtos;
using Keyloop.Inventory.Domain.Enums;
using Keyloop.Inventory.Infrastructure.Persistence;

namespace Keyloop.Inventory.Tests.Infrastructure;

public class SeedCatalogueTests
{
    [Fact]
    public void The_embedded_catalogue_loads()
    {
        var catalogue = SeedCatalogue.Default;

        Assert.NotEmpty(catalogue.Makes);
        Assert.NotEmpty(catalogue.Colours);
        Assert.NotEmpty(catalogue.Lots);
        Assert.NotEmpty(catalogue.PriorDecisions);
    }

    [Fact]
    public void Every_make_has_a_code_and_at_least_one_model()
    {
        Assert.All(SeedCatalogue.Default.Makes, make =>
        {
            Assert.False(string.IsNullOrWhiteSpace(make.Make));
            Assert.False(string.IsNullOrWhiteSpace(make.Code));
            Assert.NotEmpty(make.Models);
        });
    }

    [Fact]
    public void Every_model_has_trims_and_a_price_range_that_is_the_right_way_round()
    {
        Assert.All(SeedCatalogue.Default.Makes.SelectMany(m => m.Models), model =>
        {
            Assert.NotEmpty(model.Trims);
            Assert.True(model.LowPrice > 0);
            Assert.True(model.HighPrice > model.LowPrice);
        });
    }

    [Fact]
    public void Stock_number_prefixes_are_unique_per_make()
    {
        var codes = SeedCatalogue.Default.Makes.Select(m => m.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void Every_action_status_has_a_sample_decision_so_the_review_board_fills_out()
    {
        var covered = SeedCatalogue.Default.PriorDecisions.Select(d => d.Status).ToHashSet();

        Assert.Equal(Enum.GetValues<ActionStatus>().ToHashSet(), covered);
    }

    [Fact]
    public void A_catalogue_supplied_by_the_caller_is_the_one_used()
    {
        var catalogue = new SeedCatalogue(
            [new Dealership("TEST-01", "Test branch", 100)],
            [new SeedMake("Rivian", "RV", [new SeedModel("R1T", ["Adventure"], 70_000, 90_000)])],
            ["Forest Green"],
            ["Lot Z"],
            [new SeedDecision(ActionStatus.WholesaleOrAuction, "Test note.")]);

        var vehicles = InventorySeed.Generate(20, new DateOnly(2026, 11, 2), catalogue);

        Assert.All(vehicles, v =>
        {
            Assert.Equal("Rivian", v.Make);
            Assert.StartsWith("RV-", v.StockNumber);
            Assert.Equal("Forest Green", v.Colour);
        });
    }
}
