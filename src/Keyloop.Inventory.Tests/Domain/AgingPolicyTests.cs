using Keyloop.Inventory.Domain.Entities;

namespace Keyloop.Inventory.Tests.Domain;

public class AgingPolicyTests
{
    private static readonly DateOnly Today = new(2026, 11, 2);

    [Theory]
    [InlineData(89, false)]
    [InlineData(90, false)]
    [InlineData(91, true)]
    public void Aging_begins_strictly_after_ninety_days(int daysInStock, bool expectedAging)
    {
        var stockedOn = Today.AddDays(-daysInStock);

        var isAging = AgingPolicy.IsAging(stockedOn, Today);

        Assert.Equal(expectedAging, isAging);
    }

    [Fact]
    public void Days_in_stock_is_the_whole_number_of_days_since_stocking()
    {
        var stockedOn = Today.AddDays(-45);

        var daysInStock = AgingPolicy.DaysInStock(stockedOn, Today);

        Assert.Equal(45, daysInStock);
    }

    [Fact]
    public void A_vehicle_stocked_today_has_no_age_and_is_not_aging()
    {
        Assert.Equal(0, AgingPolicy.DaysInStock(Today, Today));
        Assert.False(AgingPolicy.IsAging(Today, Today));
    }
}
