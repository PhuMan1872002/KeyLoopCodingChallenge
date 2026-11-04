using Bunit;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;
using Keyloop.Inventory.Tests.Domain;
using Keyloop.Inventory.Web.Components.Shared;

namespace Keyloop.Inventory.Tests.Components;

public class LogActionPanelTests : BunitContext
{
    private static Vehicle Vehicle(string vin, IReadOnlyList<VehicleAction>? actions = null) =>
        TestData.Vehicle(vin, make: "Honda", model: "CR-V", daysInStock: 120, listPrice: 30_000m, actions: actions);

    [Fact]
    public void An_unrelated_rerender_on_the_same_vehicle_does_not_clear_the_draft_note()
    {
        var vehicle = Vehicle("VIN-0000001");
        var cut = Render<LogActionPanel>(p => p.Add(x => x.Selected, vehicle));

        cut.Find("textarea").Input("Reduce to clear before year-end");
        Assert.Equal("Reduce to clear before year-end", cut.Find("textarea").GetAttribute("value"));

        cut.Render(p => p.Add(x => x.Selected, vehicle));

        Assert.Equal("Reduce to clear before year-end", cut.Find("textarea").GetAttribute("value"));
    }

    [Fact]
    public void Opening_on_a_different_vehicle_does_reset_the_draft()
    {
        var cut = Render<LogActionPanel>(p => p.Add(x => x.Selected, Vehicle("VIN-0000001")));

        cut.Find("textarea").Input("A note about VIN-1");

        cut.Render(p => p.Add(x => x.Selected, Vehicle("VIN-0000002")));

        Assert.True(string.IsNullOrEmpty(cut.Find("textarea").GetAttribute("value")));
    }

    [Fact]
    public void Without_an_initial_status_the_briefs_own_example_is_preselected()
    {
        var cut = Render<LogActionPanel>(p => p.Add(x => x.Selected, Vehicle("VIN-0000001")));

        var selected = cut.Find(".option.is-selected");
        Assert.Contains("Price reduction planned", selected.TextContent);
    }

    [Fact]
    public void An_initial_status_is_preselected_over_the_default()
    {
        var cut = Render<LogActionPanel>(p => p
            .Add(x => x.Selected, Vehicle("VIN-0000001"))
            .Add(x => x.InitialStatus, ActionStatus.WholesaleOrAuction));

        var selected = cut.Find(".option.is-selected");
        Assert.Contains("Wholesale / auction", selected.TextContent);
    }

    [Fact]
    public void Changing_initial_status_on_the_same_vehicle_does_not_override_a_choice_already_made()
    {
        var cut = Render<LogActionPanel>(p => p
            .Add(x => x.Selected, Vehicle("VIN-0000001"))
            .Add(x => x.InitialStatus, ActionStatus.WholesaleOrAuction));

        cut.FindAll(".option")[0].Click();

        cut.Render(p => p
            .Add(x => x.Selected, Vehicle("VIN-0000001"))
            .Add(x => x.InitialStatus, ActionStatus.MarketingPromotion));

        var selected = cut.Find(".option.is-selected");
        Assert.Contains("Price reduction planned", selected.TextContent);
    }

    [Fact]
    public void Saving_raises_OnSave_with_the_entered_fields()
    {
        VehicleAction? saved = null;
        var cut = Render<LogActionPanel>(p => p
            .Add(x => x.Selected, Vehicle("VIN-0000001"))
            .Add(x => x.OnSave, action => saved = action));

        cut.Find("textarea").Input("Clear before year-end");
        cut.Find(".drawer__foot .btn--primary").Click();

        Assert.NotNull(saved);
        Assert.Equal(ActionStatus.PriceReductionPlanned, saved!.Status);
        Assert.Equal("Clear before year-end", saved.Note);
    }

    [Fact]
    public void A_vehicle_with_a_vin_shorter_than_eight_characters_does_not_crash_the_panel()
    {
        var cut = Render<LogActionPanel>(p => p.Add(x => x.Selected, Vehicle("AB")));

        Assert.Contains("AB", cut.Find(".drawer__id").TextContent);
    }
}
