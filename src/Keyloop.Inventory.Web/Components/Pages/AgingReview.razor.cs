using Microsoft.AspNetCore.Components;
using Keyloop.Inventory.Application.Queries;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;
using Keyloop.Inventory.Application.Interfaces;
using Keyloop.Inventory.Web.ViewModels;

namespace Keyloop.Inventory.Web.Components.Pages;

public partial class AgingReview : ComponentBase, IAsyncDisposable
{
    [Inject] private InventoryQueryService Inventory { get; set; } = default!;
    [Inject] private IInventoryNotifier Notifications { get; set; } = default!;
    [Inject] private IInventoryTelemetry Telemetry { get; set; } = default!;
    [Inject] private IBranchDirectory Branches { get; set; } = default!;
    [Inject] private ILogger<AgingReview> Logger { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "dealership")]
    public string? DealershipQuery { get; set; }

    private ActionLogWorkflow _workflow = default!;
    private IReadOnlyList<Vehicle> _aging = [];
    private string? _dealershipId;
    private Vehicle? _selected;
    private ActionStatus? _pendingStatus;
    private Vehicle? _dragging;
    private ActionStatus? _dragOverStatus;
    private bool _loading = true;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private IReadOnlyList<Vehicle> Unreviewed =>
        [.. _aging.Where(v => v.CurrentAction is null).OrderByDescending(v => v.DaysInStock(Today))];

    private int WithPlan => _aging.Count(v => v.CurrentAction is not null);

    private IEnumerable<IGrouping<(ActionStatus Status, string Label), Vehicle>> Grouped =>
        _aging.Where(v => v.CurrentAction is not null)
            .OrderByDescending(v => v.DaysInStock(Today))
            .GroupBy(v => (v.CurrentAction!.Status, v.CurrentAction!.StatusLabel))
            .OrderBy(g => g.Key.Status);

    private static bool IsOverdue(VehicleAction action) =>
        action.TargetDate is { } due && due < Today;

    private static string DueLabel(VehicleAction action) => action.TargetDate switch
    {
        null => "No review date",
        { } due when due < Today => $"Overdue — {Formats.Date(due)}",
        { } due => $"Due {Formats.Date(due)}",
    };

    protected override async Task OnInitializedAsync()
    {
        _dealershipId = BranchQuery.Decode(DealershipQuery) ?? Branches.Default.Id;
        _workflow = new ActionLogWorkflow(Inventory, Notifications, Telemetry, Logger, async () => await LoadAsync());
        _workflow.Changed += OnWorkflowChanged;
        await LoadAsync();
    }

    private void OnWorkflowChanged() => InvokeAsync(StateHasChanged);

    private async Task LoadAsync()
    {
        _loading = true;
        using var scope = Telemetry.BeginQuery("aging_review");
        try
        {
            _aging = await Inventory.GetVehiclesAsync(
                VehicleFilter.None with { DealershipId = _dealershipId, AgingOnly = true }, Today);
            scope.SetResultCount(_aging.Count);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task OnBranchChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        _dealershipId = string.IsNullOrWhiteSpace(raw) ? null : raw;
        await LoadAsync();
    }

    private void Open(Vehicle summary)
    {
        _selected = summary;
        _pendingStatus = summary.CurrentAction?.Status;
    }

    private void Close()
    {
        _selected = null;
        _pendingStatus = null;
    }

    private void OnDragStart(Vehicle vehicle) => _dragging = vehicle;

    private void OnDragEnd()
    {
        _dragging = null;
        _dragOverStatus = null;
    }

    private void OnDragLeaveColumn(ActionStatus status)
    {
        if (_dragOverStatus == status)
        {
            _dragOverStatus = null;
        }
    }

    private void OnDropOnColumn(ActionStatus status)
    {
        _dragOverStatus = null;

        if (_dragging is not { } vehicle)
        {
            return;
        }

        _dragging = null;
        _selected = vehicle;
        _pendingStatus = status;
    }

    private async Task SaveAction(VehicleAction action)
    {
        if (_selected is not { } summary)
        {
            return;
        }

        _selected = null;
        _pendingStatus = null;
        await _workflow.SaveAsync(summary.Vin, summary.StockNumber, action);
        await LoadAsync();
    }

    public ValueTask DisposeAsync()
    {
        _workflow.Changed -= OnWorkflowChanged;
        _workflow.Dispose();
        return ValueTask.CompletedTask;
    }
}
