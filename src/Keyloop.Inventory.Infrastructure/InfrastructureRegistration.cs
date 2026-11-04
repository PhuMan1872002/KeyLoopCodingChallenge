using Keyloop.Inventory.Application.Interfaces;
using Keyloop.Inventory.Application.Queries;
using Keyloop.Inventory.Infrastructure.Persistence;
using Keyloop.Inventory.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Keyloop.Inventory.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddInventoryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(configuration
            .GetSection("InventoryStore")
            .Get<InventoryStoreOptions>() ?? new InventoryStoreOptions());

        services.AddSingleton<IInventoryClient, JsonInventoryClient>();
        services.AddSingleton<IBranchDirectory, BranchDirectory>();
        services.AddSingleton<IInventoryNotifier, InventoryNotificationService>();
        services.AddSingleton<IInventoryTelemetry, InventoryTelemetry>();
        services.AddScoped<InventoryQueryService>();

        return services;
    }

    public static IReadOnlyList<string> TraceSources =>
        [JsonInventoryClient.ActivitySource.Name, InventoryTelemetry.ActivitySource.Name];

    public static string MeterName => InventoryTelemetry.MeterName;
}
