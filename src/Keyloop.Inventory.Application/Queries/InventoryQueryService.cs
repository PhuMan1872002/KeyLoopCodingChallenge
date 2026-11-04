using Keyloop.Inventory.Application.Dtos;
using Keyloop.Inventory.Application.Interfaces;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;

namespace Keyloop.Inventory.Application.Queries;

public sealed class InventoryQueryService(IInventoryClient client)
{
    private readonly IInventoryClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<IReadOnlyList<Vehicle>> GetVehiclesAsync(
        VehicleFilter filter,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var all = await _client.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return filter.Apply(all, today);
    }

    public async Task<IReadOnlyList<Vehicle>> GetSpotlightAsync(
        DateOnly today,
        int count,
        string? dealershipId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var all = await _client.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var filter = VehicleFilter.None with { DealershipId = dealershipId, NeedsActionOnly = true };

        return [.. filter.Apply(all, today).Take(count)];
    }

    public async Task<InventoryStatistics> GetStatisticsAsync(
        DateOnly today,
        string? dealershipId = null,
        CancellationToken cancellationToken = default)
    {
        var loaded = await _client.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var all = dealershipId is null
            ? loaded
            : loaded.Where(v => v.DealershipId == dealershipId).ToList();

        if (all.Count == 0)
        {
            return InventoryStatistics.Empty;
        }

        var aging = all.Where(v => v.IsAging(today)).ToList();

        var bucketCounts = all
            .GroupBy(v => AgeBuckets.Of(v.StockedOn, today))
            .ToDictionary(g => g.Key, g => g.Count());

        var byMake = aging
            .GroupBy(v => v.Make)
            .Select(g => new MakeExposure(g.Key, g.Sum(v => v.ListPrice), g.Count()))
            .OrderByDescending(m => m.Value)
            .Take(6)
            .ToList();

        return new InventoryStatistics(
            TotalVehicles: all.Count,
            TotalStockValue: all.Sum(v => v.ListPrice),
            AverageDaysInStock: all.Average(v => v.DaysInStock(today)),
            AgingVehicles: aging.Count,
            AgingStockValue: aging.Sum(v => v.ListPrice),
            AgingWithoutPlan: aging.Count(v => v.CurrentAction is null),
            BucketCounts: bucketCounts,
            AgingByMake: byMake);
    }

    public Task LogActionAsync(
        string vin,
        VehicleAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);
        ArgumentNullException.ThrowIfNull(action);

        return _client.AppendActionAsync(vin, action, cancellationToken);
    }
}
