using Keyloop.Inventory.Application.Exceptions;
using Keyloop.Inventory.Application.Queries;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Application.Dtos;
using Keyloop.Inventory.Application.Interfaces;

namespace Keyloop.Inventory.Web.ViewModels;

public sealed class ActionLogWorkflow : IDisposable
{
    private static readonly TimeSpan DefaultToastDuration = TimeSpan.FromSeconds(4);

    private readonly InventoryQueryService _inventory;
    private readonly IInventoryNotifier _notifications;
    private readonly IInventoryTelemetry _telemetry;
    private readonly ILogger _logger;
    private readonly Func<Task> _toastLifetime;
    private readonly Func<Task> _onExternalChange;
    private readonly IDisposable _subscription;

    private int _toastGeneration;

    public ActionLogWorkflow(
        InventoryQueryService inventory,
        IInventoryNotifier notifications,
        IInventoryTelemetry telemetry,
        ILogger logger,
        Func<Task> onExternalChange,
        Func<Task>? toastLifetime = null)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _onExternalChange = onExternalChange ?? throw new ArgumentNullException(nameof(onExternalChange));
        _toastLifetime = toastLifetime ?? (() => Task.Delay(DefaultToastDuration));
        _subscription = notifications.Subscribe(OnChangedAsync);
    }

    public string CircuitId { get; } = Guid.NewGuid().ToString("N");

    public string? Toast { get; private set; }

    public event Action? Changed;

    private async Task OnChangedAsync(VehicleChanged change)
    {
        if (change.OriginCircuitId == CircuitId)
        {
            return;
        }

        await _onExternalChange().ConfigureAwait(false);
        Changed?.Invoke();
    }

    public async Task SaveAsync(string vin, string stockNumber, VehicleAction action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            await _inventory.LogActionAsync(vin, action).ConfigureAwait(false);
            _telemetry.RecordActionLogged(action.Status.ToString());
            Toast = $"{action.StatusLabel} saved for {stockNumber}";

            await _notifications.PublishAsync(new VehicleChanged(vin, CircuitId)).ConfigureAwait(false);
        }
        catch (VehicleNotFoundException ex)
        {
            _telemetry.RecordActionFailed("vehicle_not_found");
            _logger.LogWarning(ex, "Action rejected for unknown VIN {Vin}", vin);
            Toast = "That vehicle is no longer in inventory.";
        }

        Changed?.Invoke();

        PendingToastDismissal = DismissToastAsync(++_toastGeneration);
    }

    internal Task PendingToastDismissal { get; private set; } = Task.CompletedTask;

    private async Task DismissToastAsync(int generation)
    {
        await _toastLifetime().ConfigureAwait(false);

        if (generation == _toastGeneration)
        {
            Toast = null;
            Changed?.Invoke();
        }
    }

    public void Dispose() => _subscription.Dispose();
}
