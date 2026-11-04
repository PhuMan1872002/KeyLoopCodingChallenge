using Keyloop.Inventory.Application.Dtos;

namespace Keyloop.Inventory.Application.Interfaces;

public interface IInventoryNotifier
{
    IDisposable Subscribe(Func<VehicleChanged, Task> handler);

    Task PublishAsync(VehicleChanged change);
}
