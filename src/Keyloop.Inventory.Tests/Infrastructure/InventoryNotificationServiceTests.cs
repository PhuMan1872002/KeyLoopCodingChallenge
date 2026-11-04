using Microsoft.Extensions.Logging.Abstractions;
using Keyloop.Inventory.Application.Dtos;
using Keyloop.Inventory.Infrastructure.Services;

namespace Keyloop.Inventory.Tests.Infrastructure;

public class InventoryNotificationServiceTests
{
    private static InventoryNotificationService Create() => new(NullLogger<InventoryNotificationService>.Instance);

    [Fact]
    public async Task Every_subscriber_is_notified_of_a_change()
    {
        var service = Create();
        var received = new List<string>();

        using var first = service.Subscribe(c => { received.Add("first:" + c.Vin); return Task.CompletedTask; });
        using var second = service.Subscribe(c => { received.Add("second:" + c.Vin); return Task.CompletedTask; });

        await service.PublishAsync(new VehicleChanged("VIN-1", "circuit-a"));

        Assert.Equal(["first:VIN-1", "second:VIN-1"], received);
    }

    [Fact]
    public async Task A_subscriber_throwing_ObjectDisposedException_does_not_stop_the_rest()
    {
        var service = Create();
        var reached = false;

        using var torndown = service.Subscribe(_ => throw new ObjectDisposedException("circuit"));
        using var still = service.Subscribe(_ => { reached = true; return Task.CompletedTask; });

        await service.PublishAsync(new VehicleChanged("VIN-1", "circuit-a"));

        Assert.True(reached);
    }

    [Fact]
    public async Task A_subscriber_throwing_any_other_exception_still_does_not_stop_the_rest()
    {
        var service = Create();
        var reached = new List<string>();

        using var before = service.Subscribe(c => { reached.Add("before"); return Task.CompletedTask; });
        using var failing = service.Subscribe(_ => throw new InvalidOperationException("unexpected"));
        using var after = service.Subscribe(c => { reached.Add("after"); return Task.CompletedTask; });

        await service.PublishAsync(new VehicleChanged("VIN-1", "circuit-a"));

        Assert.Equal(["before", "after"], reached);
    }

    [Fact]
    public async Task Disposing_a_subscription_stops_further_notifications()
    {
        var service = Create();
        var count = 0;

        var subscription = service.Subscribe(_ => { count++; return Task.CompletedTask; });
        subscription.Dispose();

        await service.PublishAsync(new VehicleChanged("VIN-1", "circuit-a"));

        Assert.Equal(0, count);
    }
}
