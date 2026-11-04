using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;
using Keyloop.Inventory.Infrastructure.Persistence;
using Keyloop.Inventory.Tests.Domain;
using Keyloop.Inventory.Web.Components.Pages;
using Keyloop.Inventory.Web.ViewModels;

namespace Keyloop.Inventory.Tests.Components;

public class AgingReviewTests : ComponentTest
{
    [Fact]
    public void Unreviewed_and_reviewed_vehicles_land_in_separate_columns()
    {
        var planned = TestData.Action(ActionStatus.PriceReductionPlanned);
        Arrange(TestData.Aging("VIN-00000001"), TestData.Aging("VIN-00000002", actions: [planned]));

        var cut = Render<AgingReview>();

        Assert.Contains("Not reviewed", cut.Markup);
        Assert.Contains("Price reduction planned", cut.Markup);
    }

    [Fact]
    public void Dragging_an_unreviewed_card_onto_a_status_column_opens_the_panel_preselected_to_it()
    {
        var planned = TestData.Action(ActionStatus.MarketingPromotion);
        Arrange(
            TestData.Aging("VIN-00000001"),
            TestData.Aging("VIN-00000002", actions: [planned]));

        var cut = Render<AgingReview>();

        cut.Find(".board__col--unreviewed .card").TriggerEvent("ondragstart", new DragEventArgs());
        cut.Find("[aria-label='Marketing promotion']").TriggerEvent("ondrop", new DragEventArgs());

        var selected = cut.Find(".option.is-selected");
        Assert.Contains("Marketing promotion", selected.TextContent);
    }

    [Fact]
    public void Dropping_outside_a_drag_does_nothing()
    {
        var planned = TestData.Action(ActionStatus.MarketingPromotion);
        Arrange(TestData.Aging("VIN-00000001", actions: [planned]));

        var cut = Render<AgingReview>();

        cut.Find("[aria-label='Marketing promotion']").TriggerEvent("ondrop", new DragEventArgs());

        Assert.Empty(cut.FindAll(".drawer"));
    }

    [Fact]
    public void Reopening_a_reviewed_card_preselects_its_current_status()
    {
        var planned = TestData.Action(ActionStatus.WholesaleOrAuction);
        Arrange(TestData.Aging("VIN-00000001", actions: [planned]));

        var cut = Render<AgingReview>();
        cut.Find(".btn--row").Click();

        var selected = cut.Find(".option.is-selected");
        Assert.Contains("Wholesale / auction", selected.TextContent);
    }

    [Fact]
    public void With_no_branch_in_the_query_the_default_branch_is_shown()
    {
        Arrange(
            TestData.Aging("VIN-DOWNTOWN", days: 120),
            TestData.Vehicle("VIN-EASTSIDE", daysInStock: 120, dealershipId: TestData.Branches.All[1].Id));

        var cut = Render<AgingReview>();

        Assert.Contains("VIN-DOWNTOWN"[^8..], cut.Markup);
        Assert.DoesNotContain("VIN-EASTSIDE"[^8..], cut.Markup);
    }

    [Fact]
    public void Selecting_all_branches_in_the_dropdown_shows_every_branch()
    {
        Arrange(
            TestData.Aging("VIN-DOWNTOWN", days: 120),
            TestData.Vehicle("VIN-EASTSIDE", daysInStock: 120, dealershipId: TestData.Branches.All[1].Id));

        var cut = Render<AgingReview>();
        Assert.DoesNotContain("VIN-EASTSIDE"[^8..], cut.Markup);

        cut.Find(".field select").Change(string.Empty);

        Assert.Contains("VIN-DOWNTOWN"[^8..], cut.Markup);
        Assert.Contains("VIN-EASTSIDE"[^8..], cut.Markup);
    }

    [Fact]
    public void BranchQuery_round_trips_a_branch_id_and_the_all_branches_sentinel()
    {
        Assert.Equal(TestData.Branches.Default.Id, BranchQuery.Decode(BranchQuery.Encode(TestData.Branches.Default.Id)));
        Assert.Null(BranchQuery.Decode(BranchQuery.Encode(null)));
        Assert.Equal(BranchQuery.All, BranchQuery.Encode(null));
        Assert.Null(BranchQuery.Decode(BranchQuery.All));
        Assert.Null(BranchQuery.Decode(null));
    }
}
