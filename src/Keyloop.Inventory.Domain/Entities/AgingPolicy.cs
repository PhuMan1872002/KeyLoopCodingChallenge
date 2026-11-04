namespace Keyloop.Inventory.Domain.Entities;

public static class AgingPolicy
{
    public const int AgingThresholdDays = 90;

    public static int DaysInStock(DateOnly stockedOn, DateOnly today)
        => today.DayNumber - stockedOn.DayNumber;

    public static bool IsAging(DateOnly stockedOn, DateOnly today)
        => DaysInStock(stockedOn, today) > AgingThresholdDays;
}
