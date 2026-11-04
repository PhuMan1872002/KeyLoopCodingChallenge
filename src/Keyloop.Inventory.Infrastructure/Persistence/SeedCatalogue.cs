using Keyloop.Inventory.Application.Dtos;
using System.Reflection;
using System.Text.Json;
using Keyloop.Inventory.Domain.Enums;

namespace Keyloop.Inventory.Infrastructure.Persistence;

public sealed record SeedModel(string Model, string[] Trims, int LowPrice, int HighPrice);

public sealed record SeedMake(string Make, string Code, SeedModel[] Models);

public sealed record SeedDecision(ActionStatus Status, string Note);

public sealed record SeedCatalogue(
    Dealership[] Branches,
    SeedMake[] Makes,
    string[] Colours,
    string[] Lots,
    SeedDecision[] PriorDecisions)
{
    private const string ResourceName =
        "Keyloop.Inventory.Infrastructure.Persistence.catalogue.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public static SeedCatalogue Default { get; } = Load();

    private static SeedCatalogue Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The seed catalogue '{ResourceName}' is not embedded in the assembly.");

        return JsonSerializer.Deserialize<SeedCatalogue>(stream, Options)
            ?? throw new InvalidOperationException("The seed catalogue deserialised to null.");
    }
}
