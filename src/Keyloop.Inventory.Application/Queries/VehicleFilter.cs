using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;

namespace Keyloop.Inventory.Application.Queries;

public sealed record VehicleFilter
{
    public string? DealershipId { get; init; }
    public string? Make { get; init; }
    public string? Model { get; init; }
    public AgeBucket? AgeBucket { get; init; }
    public string? Search { get; init; }
    public bool AgingOnly { get; init; }
    public bool NeedsActionOnly { get; init; }
    public SortOrder Sort { get; init; } = SortOrder.OldestFirst;

    public static VehicleFilter None { get; } = new();

    public bool IsEmpty =>
        DealershipId is null && Make is null && Model is null && AgeBucket is null
        && string.IsNullOrWhiteSpace(Search) && !AgingOnly && !NeedsActionOnly;

    public IReadOnlyList<Vehicle> Apply(IEnumerable<Vehicle> source, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(source);

        var term = Search?.Trim();

        var matched = source
            .WhereIf(DealershipId is not null, v => v.DealershipId == DealershipId)
            .WhereIf(Make is not null, v => Matches(v.Make, Make))
            .WhereIf(Model is not null, v => Matches(v.Model, Model))
            .WhereIf(!string.IsNullOrWhiteSpace(term), v => MatchesSearch(v, term!))
            .WhereIf(AgeBucket is not null, v => AgeBuckets.Of(v.StockedOn, today) == AgeBucket)
            .WhereIf(AgingOnly || NeedsActionOnly, v => v.IsAging(today))
            .WhereIf(NeedsActionOnly, v => v.CurrentAction is null);

        return [.. Order(matched)];
    }

    private IOrderedEnumerable<Vehicle> Order(IEnumerable<Vehicle> matched) => Sort switch
    {
        SortOrder.NewestFirst => matched.OrderByDescending(v => v.StockedOn),
        SortOrder.PriceHighToLow => matched.OrderByDescending(v => v.ListPrice),
        _ => matched.OrderBy(v => v.StockedOn),
    };

    private static bool Matches(string value, string? selected) =>
        string.Equals(value, selected, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesSearch(Vehicle v, string term) =>
        v.StockNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
        || v.Vin.Contains(term, StringComparison.OrdinalIgnoreCase)
        || v.Make.Contains(term, StringComparison.OrdinalIgnoreCase)
        || v.Model.Contains(term, StringComparison.OrdinalIgnoreCase);
}
