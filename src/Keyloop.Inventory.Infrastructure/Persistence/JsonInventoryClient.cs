using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using Keyloop.Inventory.Application.Exceptions;
using Keyloop.Inventory.Application.Interfaces;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;

namespace Keyloop.Inventory.Infrastructure.Persistence;

public sealed class JsonInventoryClient : IInventoryClient, IDisposable
{
    public static readonly ActivitySource ActivitySource = new("Keyloop.Inventory");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly InventoryStoreOptions _options;
    private readonly ILogger<JsonInventoryClient> _logger;
    private readonly string _absolutePath;

    private IReadOnlyList<Vehicle>? _snapshot;

    public JsonInventoryClient(
        InventoryStoreOptions options,
        IHostEnvironment environment,
        ILogger<JsonInventoryClient> logger)
    {
        _options = options;
        _logger = logger;
        _absolutePath = Path.IsPathRooted(options.FilePath)
            ? options.FilePath
            : Path.Combine(environment.ContentRootPath, options.FilePath);
    }

    public async Task<IReadOnlyList<Vehicle>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_snapshot is { } cached)
        {
            return cached;
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _snapshot ??= await LoadOrSeedAsync(cancellationToken).ConfigureAwait(false);
            return _snapshot;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task AppendActionAsync(
        string vin,
        VehicleAction action,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("inventory.action.append");
        activity?.SetTag("vehicle.vin", vin);
        activity?.SetTag("action.status", action.Status.ToString());

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var vehicles = (_snapshot ??= await LoadOrSeedAsync(cancellationToken).ConfigureAwait(false)).ToList();

            var index = vehicles.FindIndex(v => v.Vin == vin);
            if (index < 0)
            {
                throw new VehicleNotFoundException(vin);
            }

            vehicles[index] = vehicles[index] with
            {
                Actions = [.. vehicles[index].Actions, action],
            };

            _snapshot = vehicles;
            await PersistAsync(vehicles, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Action {ActionStatus} logged against vehicle {Vin}",
                action.Status,
                vin);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<IReadOnlyList<Vehicle>> LoadOrSeedAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_absolutePath))
        {
            List<Vehicle>? loaded = null;

            try
            {
                await using var stream = File.OpenRead(_absolutePath);
                loaded = await JsonSerializer
                    .DeserializeAsync<List<Vehicle>>(stream, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Inventory store at {StorePath} could not be read and will be regenerated",
                    _absolutePath);
            }

            if (loaded is { Count: > 0 })
            {
                _logger.LogInformation(
                    "Loaded {VehicleCount} vehicles from {StorePath}",
                    loaded.Count,
                    _absolutePath);
                return loaded;
            }

            if (loaded is not null)
            {
                _logger.LogWarning("Inventory store at {StorePath} was empty; reseeding", _absolutePath);
            }
        }

        var seeded = InventorySeed.Generate(
            _options.SeedVehicleCount,
            DateOnly.FromDateTime(DateTime.UtcNow));

        await PersistAsync(seeded, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Seeded {VehicleCount} vehicles into {StorePath}",
            seeded.Count,
            _absolutePath);

        return seeded;
    }

    private async Task PersistAsync(IReadOnlyList<Vehicle> vehicles, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_absolutePath)!);

        var temporaryPath = _absolutePath + ".tmp";

        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer
                .SerializeAsync(stream, vehicles, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(temporaryPath, _absolutePath, overwrite: true);
    }

    public void Dispose() => _writeLock.Dispose();
}
