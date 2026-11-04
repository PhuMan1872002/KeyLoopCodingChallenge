namespace Keyloop.Inventory.Infrastructure.Persistence;

public sealed class InventoryStoreOptions
{
    public string FilePath { get; set; } = Path.Combine("App_Data", "inventory.json");

    public int SeedVehicleCount { get; set; } = 150;
}
