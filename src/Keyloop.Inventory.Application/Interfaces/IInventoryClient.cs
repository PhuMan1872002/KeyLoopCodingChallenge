using Keyloop.Inventory.Domain.Entities;

namespace Keyloop.Inventory.Application.Interfaces;

public interface IInventoryClient
{
    Task<IReadOnlyList<Vehicle>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AppendActionAsync(string vin, VehicleAction action, CancellationToken cancellationToken = default);
}
