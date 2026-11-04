using System.Text.Json.Serialization;
using Keyloop.Inventory.Domain.Enums;

namespace Keyloop.Inventory.Domain.Entities;

public sealed record Vehicle
{
    public required string Vin { get; init; }

    public required string StockNumber { get; init; }
    public required string Make { get; init; }
    public required string Model { get; init; }
    public required string Trim { get; init; }
    public required int ModelYear { get; init; }
    public required int Mileage { get; init; }
    public required decimal ListPrice { get; init; }

    public required string Colour { get; init; }

    public required string Location { get; init; }

    public required DateOnly StockedOn { get; init; }

    public required string DealershipId { get; init; }

    public IReadOnlyList<VehicleAction> Actions { get; init; } = [];

    [JsonIgnore]
    public VehicleAction? CurrentAction =>
        Actions.Count == 0 ? null : Actions.MaxBy(a => a.RecordedAt);

    public int DaysInStock(DateOnly today) => AgingPolicy.DaysInStock(StockedOn, today);

    public bool IsAging(DateOnly today) => AgingPolicy.IsAging(StockedOn, today);

    public AgeBand AgeBandOn(DateOnly today) => AgeBands.Of(StockedOn, today);

    public bool NeedsAction(DateOnly today) => IsAging(today) && CurrentAction is null;
}
