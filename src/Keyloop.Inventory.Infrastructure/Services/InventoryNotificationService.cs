using Keyloop.Inventory.Application.Dtos;
using Keyloop.Inventory.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyloop.Inventory.Infrastructure.Services;

public sealed class InventoryNotificationService(ILogger<InventoryNotificationService> logger)
    : IInventoryNotifier
{
    private readonly Lock _gate = new();
    private readonly List<Func<VehicleChanged, Task>> _subscribers = [];

    public IDisposable Subscribe(Func<VehicleChanged, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            _subscribers.Add(handler);
        }

        return new Subscription(this, handler);
    }

    public async Task PublishAsync(VehicleChanged change)
    {
        Func<VehicleChanged, Task>[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _subscribers];
        }

        foreach (var subscriber in snapshot)
        {
            try
            {
                await subscriber(change).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException))
            {
                logger.LogWarning(ex, "A subscriber failed to receive {VehicleChanged}; continuing with the rest", change);
            }
        }
    }

    private void Unsubscribe(Func<VehicleChanged, Task> handler)
    {
        lock (_gate)
        {
            _subscribers.Remove(handler);
        }
    }

    private sealed class Subscription(InventoryNotificationService owner, Func<VehicleChanged, Task> handler)
        : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.Unsubscribe(handler);
        }
    }
}
