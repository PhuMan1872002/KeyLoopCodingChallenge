using Keyloop.Inventory.Domain.Entities;


namespace Keyloop.Inventory.Domain.Enums;

public enum AgeBand
{
    Fresh = 1,
    DueSoon = 2,
    Aging = 3,
    Critical = 4,
}

public static class AgeBands
{
    public const int FreshThresholdDays = 75;
    public const int CriticalThresholdDays = 150;

    public static AgeBand Of(DateOnly stockedOn, DateOnly today)
    {
        var days = AgingPolicy.DaysInStock(stockedOn, today);

        if (!AgingPolicy.IsAging(stockedOn, today))
        {
            return days <= FreshThresholdDays ? AgeBand.Fresh : AgeBand.DueSoon;
        }

        return days <= CriticalThresholdDays ? AgeBand.Aging : AgeBand.Critical;
    }

    public static string Label(this AgeBand band) => band switch
    {
        AgeBand.Fresh => "Fresh",
        AgeBand.DueSoon => "Due soon",
        AgeBand.Aging => "Aging",
        AgeBand.Critical => "Critical",
        _ => band.ToString(),
    };

    public static string Range(this AgeBand band) => band switch
    {
        AgeBand.Fresh => $"0–{FreshThresholdDays} days",
        AgeBand.DueSoon => $"{FreshThresholdDays + 1}–{AgingPolicy.AgingThresholdDays} days",
        AgeBand.Aging => $"{AgingPolicy.AgingThresholdDays + 1}–{CriticalThresholdDays} days",
        AgeBand.Critical => $"over {CriticalThresholdDays} days",
        _ => string.Empty,
    };
}
