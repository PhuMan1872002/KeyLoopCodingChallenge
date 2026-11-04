using Keyloop.Inventory.Domain.Enums;

namespace Keyloop.Inventory.Application.Dtos;

public sealed record InventoryStatistics(
    int TotalVehicles,
    decimal TotalStockValue,
    double AverageDaysInStock,
    int AgingVehicles,
    decimal AgingStockValue,
    int AgingWithoutPlan,
    IReadOnlyDictionary<AgeBucket, int> BucketCounts,
    IReadOnlyList<MakeExposure> AgingByMake)
{
    public double AgingShare => TotalVehicles == 0 ? 0 : (double)AgingVehicles / TotalVehicles;

    public int CountIn(AgeBucket bucket) => BucketCounts.TryGetValue(bucket, out var count) ? count : 0;

    public static InventoryStatistics Empty { get; } = new(
        0, 0m, 0, 0, 0m, 0, new Dictionary<AgeBucket, int>(), []);
}
