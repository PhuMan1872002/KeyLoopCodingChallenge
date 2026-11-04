using Keyloop.Inventory.Domain.Entities;


namespace Keyloop.Inventory.Domain.Enums;

public enum AgeBucket
{
    UpTo30 = 1,
    From31To60 = 2,
    From61ToThreshold = 3,
    OverThreshold = 4,
}

public static class AgeBuckets
{
    public static AgeBucket Of(DateOnly stockedOn, DateOnly today)
    {
        if (AgingPolicy.IsAging(stockedOn, today))
        {
            return AgeBucket.OverThreshold;
        }

        return AgingPolicy.DaysInStock(stockedOn, today) switch
        {
            <= 30 => AgeBucket.UpTo30,
            <= 60 => AgeBucket.From31To60,
            _ => AgeBucket.From61ToThreshold,
        };
    }

    public static string Label(this AgeBucket bucket) => bucket switch
    {
        AgeBucket.UpTo30 => "0–30 days",
        AgeBucket.From31To60 => "31–60 days",
        AgeBucket.From61ToThreshold => $"61–{AgingPolicy.AgingThresholdDays} days",
        AgeBucket.OverThreshold => $"Over {AgingPolicy.AgingThresholdDays} days",
        _ => bucket.ToString(),
    };

    public static string Tag(this AgeBucket bucket) => bucket switch
    {
        AgeBucket.OverThreshold => "Aging",
        AgeBucket.From61ToThreshold => "Watch",
        _ => "Healthy",
    };
}
