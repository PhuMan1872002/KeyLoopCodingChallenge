using Bunit;
using Keyloop.Inventory.Domain.Enums;
using Keyloop.Inventory.Web.Components.Shared;

namespace Keyloop.Inventory.Tests.Components;

public class AgingBadgeTests : BunitContext
{
    [Fact]
    public void An_aging_vehicle_is_visibly_flagged()
    {
        var cut = Render<AgingBadge>(parameters => parameters
            .Add(p => p.DaysInStock, 120)
            .Add(p => p.IsAging, true)
            .Add(p => p.Band, AgeBand.Aging));

        Assert.NotNull(cut.Find("[data-testid='aging-flag']"));
        Assert.Contains("120 d", cut.Markup);
    }

    [Fact]
    public void A_vehicle_within_the_threshold_carries_no_flag()
    {
        var cut = Render<AgingBadge>(parameters => parameters
            .Add(p => p.DaysInStock, 90)
            .Add(p => p.IsAging, false)
            .Add(p => p.Band, AgeBand.DueSoon));

        Assert.Empty(cut.FindAll("[data-testid='aging-flag']"));
        Assert.Contains("90 d", cut.Markup);
    }

    [Fact]
    public void A_vehicle_approaching_the_threshold_reads_as_due_soon()
    {
        var cut = Render<AgingBadge>(parameters => parameters
            .Add(p => p.DaysInStock, 90)
            .Add(p => p.IsAging, false)
            .Add(p => p.Band, AgeBand.DueSoon));

        Assert.Contains("Due soon", cut.Find("[data-testid='age-band']").TextContent);
    }

    [Fact]
    public void Critical_stock_still_carries_the_aging_flag()
    {
        var cut = Render<AgingBadge>(parameters => parameters
            .Add(p => p.DaysInStock, 188)
            .Add(p => p.IsAging, true)
            .Add(p => p.Band, AgeBand.Critical));

        Assert.Contains("Critical", cut.Find("[data-testid='aging-flag']").TextContent);
    }
}
