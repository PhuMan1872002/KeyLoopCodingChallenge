using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;
using Keyloop.Inventory.Infrastructure.Persistence;
using Keyloop.Inventory.Tests.Domain;
using Keyloop.Inventory.Web.Components.Pages;

namespace Keyloop.Inventory.Tests.Components;

public class DashboardTests : ComponentTest
{
    [Fact]
    public void The_dashboard_renders_the_headline_figures()
    {
        Arrange(
            TestData.Vehicle("VIN-FRESH", daysInStock: 10, listPrice: 20_000m),
            TestData.Aging("VIN-AGING-1"),
            TestData.Aging("VIN-AGING-2", days: 200));

        var cut = Render<Dashboard>();

        var markup = cut.Markup;

        Assert.Contains("Vehicles in stock", markup);
        Assert.Contains("Aging stock", markup);
        Assert.Contains("2 aging units without an action plan", markup);
    }

    [Fact]
    public void Aging_vehicles_are_flagged_in_the_rendered_list()
    {
        Arrange(
            TestData.Vehicle("VIN-FRESH", daysInStock: 10),
            TestData.Aging("VIN-AGING-1"));

        var cut = Render<Dashboard>();

        Assert.Single(cut.FindAll(".tbody [data-testid='aging-flag']"));
    }

    [Fact]
    public void A_vehicle_with_no_decision_offers_one_and_a_reviewed_vehicle_offers_an_update()
    {
        var planned = TestData.Action(ActionStatus.PriceReductionPlanned);

        Arrange(
            TestData.Aging("VIN-BARE"),
            TestData.Aging("VIN-PLANNED", actions: [planned]));

        var cut = Render<Dashboard>();
        var markup = cut.Markup;

        Assert.Contains("Log action", markup);
        Assert.Contains("Update", markup);
        Assert.Contains("Not reviewed", markup);
        Assert.Contains("Price reduction planned", markup);
    }

    [Fact]
    public void The_spotlight_disappears_once_every_aging_vehicle_has_a_decision()
    {
        var planned = TestData.Action(ActionStatus.WholesaleOrAuction);

        Arrange(
            TestData.Vehicle("VIN-FRESH", daysInStock: 10),
            TestData.Aging("VIN-PLANNED", actions: [planned]));

        var cut = Render<Dashboard>();

        Assert.Contains("Every aging vehicle on this lot has a logged decision.", cut.Markup);
    }

    [Fact]
    public void Choosing_an_age_band_on_the_chart_filters_the_list()
    {
        Arrange(
            TestData.Vehicle("VIN-FRESH", daysInStock: 10),
            TestData.Aging("VIN-AGING-1"),
            TestData.Aging("VIN-AGING-2", days: 200));

        var cut = Render<Dashboard>();

        Assert.Equal("Showing 3 of 3 vehicles · 2 aging", CountNote(cut));

        cut.FindAll(".chart__col")[3].Click();

        Assert.Equal("Showing 2 of 3 vehicles · 2 aging", CountNote(cut));
    }

    private static string CountNote(IRenderedComponent<Dashboard> cut) =>
        string.Join(
            ' ',
            cut.Find(".count-note").TextContent.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    [Fact]
    public void Logging_a_decision_updates_the_figures_without_a_reload()
    {
        Arrange(TestData.Aging("VIN-BARE"));

        var cut = Render<Dashboard>();
        Assert.Contains("1 aging units without an action plan", cut.Markup);

        cut.Find(".btn--row").Click();
        cut.Find(".drawer__foot .btn--primary").Click();

        Assert.Contains("0 aging units without an action plan", cut.Markup);
        Assert.Contains("saved for", cut.Markup);
    }

    [Fact]
    public void Viewing_all_aging_vehicles_navigates_to_the_review_board_for_the_selected_branch()
    {
        Arrange(TestData.Aging("VIN-AGING-1"));

        var cut = Render<Dashboard>();
        cut.Find(".btn--outline").Click();

        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        Assert.Equal($"/aging-review?dealership={TestData.Branches.Default.Id}", navigation.History.Last().Uri);
    }
}
