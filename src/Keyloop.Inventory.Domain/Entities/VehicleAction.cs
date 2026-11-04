using System.Text.Json.Serialization;
using Keyloop.Inventory.Domain.Enums;

namespace Keyloop.Inventory.Domain.Entities;

public sealed record VehicleAction
{
    public required ActionStatus Status { get; init; }
    public string? Note { get; init; }
    public decimal? ProposedPrice { get; init; }
    public DateOnly? TargetDate { get; init; }
    public required DateTimeOffset RecordedAt { get; init; }

    [JsonIgnore]
    public string StatusLabel => Status.Label();
}
