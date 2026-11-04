namespace Keyloop.Inventory.Application.Exceptions;

public sealed class VehicleNotFoundException(string vin)
    : Exception($"No vehicle with VIN '{vin}' is in inventory.")
{
    public string Vin { get; } = vin;
}
