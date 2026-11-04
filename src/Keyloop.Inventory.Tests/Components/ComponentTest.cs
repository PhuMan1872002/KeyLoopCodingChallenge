using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Keyloop.Inventory.Application.Interfaces;
using Keyloop.Inventory.Application.Queries;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Infrastructure.Persistence;
using Keyloop.Inventory.Infrastructure.Services;
using Keyloop.Inventory.Tests.Domain;

namespace Keyloop.Inventory.Tests.Components;

public abstract class ComponentTest : BunitContext
{
    protected static DateOnly Today => TestData.Today;

    protected void Arrange(params Vehicle[] vehicles)
    {
        Services.AddSingleton<IInventoryClient>(new TestData.FakeInventoryClient(vehicles));
        Services.AddSingleton<InventoryQueryService>();
        Services.AddSingleton<IInventoryNotifier, InventoryNotificationService>();
        Services.AddSingleton<IInventoryTelemetry, InventoryTelemetry>();
        Services.AddSingleton<IBranchDirectory, BranchDirectory>();
    }
}
