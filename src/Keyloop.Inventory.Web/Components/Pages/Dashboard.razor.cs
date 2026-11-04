using Microsoft.AspNetCore.Components;
using Keyloop.Inventory.Application.Dtos;
using Keyloop.Inventory.Application.Queries;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;
using Keyloop.Inventory.Application.Interfaces;
using Keyloop.Inventory.Web.ViewModels;

namespace Keyloop.Inventory.Web.Components.Pages;

public partial class Dashboard : ComponentBase, IAsyncDisposable
{
    [Inject] private InventoryQueryService Inventory { get; set; } = default!;
    [Inject] private IInventoryNotifier Notifications { get; set; } = default!;
    [Inject] private IInventoryTelemetry Telemetry { get; set; } = default!;
    [Inject] private IBranchDirectory Branches { get; set; } = default!;
    [Inject] private ILogger<Dashboard> Logger { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private const int SpotlightSize = 4;
    private const int ChartHeight = 170;

    private static readonly AgeBucket[] AllBuckets =
        [AgeBucket.UpTo30, AgeBucket.From31To60, AgeBucket.From61ToThreshold, AgeBucket.OverThreshold];

    private ActionLogWorkflow _workflow = default!;

    private List<Vehicle> _vehicles = [];
    private IReadOnlyList<Vehicle> _spotlight = [];
    private IReadOnlyList<string> _makes = [];
    private IReadOnlyList<string> _models = [];
    private InventoryStatistics? _statistics;
    private VehicleFilter _filter = VehicleFilter.None;
    private Vehicle? _selected;
    private bool _loading = true;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private int MaxBucketCount =>
        _statistics is null ? 0 : AllBuckets.Select(_statistics.CountIn).DefaultIfEmpty(0).Max();

    private int BarHeight(int count) =>
        MaxBucketCount == 0 ? 4 : Math.Max(4, (int)Math.Round((double)ChartHeight * count / MaxBucketCount));

    private static int Share(decimal value, IReadOnlyList<MakeExposure> all)
    {
        var top = all.Count == 0 ? 0m : all.Max(m => m.Value);
        return top == 0 ? 0 : (int)Math.Round(100m * value / top);
    }

    private string DistributionLabel => _statistics is null
        ? "Age distribution"
        : string.Join(", ", AllBuckets.Select(b => $"{b.Label()}: {_statistics.CountIn(b)}"));

    private string CsvHref
    {
        get
        {
            var parts = new List<string>();
            void Add(string key, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parts.Add($"{key}={Uri.EscapeDataString(value)}");
                }
            }

            Add("dealership", _filter.DealershipId);
            Add("make", _filter.Make);
            Add("model", _filter.Model);
            Add("bucket", _filter.AgeBucket?.ToString());
            Add("q", _filter.Search);
            Add("sort", _filter.Sort.ToString());
            if (_filter.AgingOnly) { parts.Add("agingOnly=true"); }

            return "/inventory.csv" + (parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts));
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _filter = _filter with { DealershipId = Branches.Default.Id };
        _workflow = new ActionLogWorkflow(Inventory, Notifications, Telemetry, Logger, async () => await LoadAsync());
        _workflow.Changed += OnWorkflowChanged;
        await LoadAsync();
    }

    private void OnWorkflowChanged() => InvokeAsync(StateHasChanged);

    private async Task LoadAsync()
    {
        _loading = true;

        using var scope = Telemetry.BeginQuery(DescribeFilter());
        try
        {
            var today = Today;
            _vehicles = [.. await Inventory.GetVehiclesAsync(_filter, today)];
            _statistics = await Inventory.GetStatisticsAsync(today, _filter.DealershipId);
            _spotlight = await Inventory.GetSpotlightAsync(today, SpotlightSize, _filter.DealershipId);

            var branchOnly = VehicleFilter.None with { DealershipId = _filter.DealershipId };
            var all = await Inventory.GetVehiclesAsync(branchOnly, today);
            _makes = [.. all.Select(v => v.Make).Distinct().Order()];
            _models = [.. all.Where(v => _filter.Make is null || v.Make == _filter.Make)
                             .Select(v => v.Model).Distinct().Order()];

            scope.SetResultCount(_vehicles.Count);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ApplyAsync(VehicleFilter filter)
    {
        _filter = filter;
        await LoadAsync();
    }

    private static string? Value(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    private Task OnBranchChanged(ChangeEventArgs e) =>
        ApplyAsync(VehicleFilter.None with { DealershipId = Value(e), Sort = _filter.Sort });

    private Task OnMakeChanged(ChangeEventArgs e) =>
        ApplyAsync(_filter with { Make = Value(e), Model = null });

    private Task OnModelChanged(ChangeEventArgs e) => ApplyAsync(_filter with { Model = Value(e) });

    private Task OnSearchChanged(ChangeEventArgs e) => ApplyAsync(_filter with { Search = Value(e) });

    private Task OnBucketChanged(ChangeEventArgs e) =>
        ApplyAsync(_filter with
        {
            AgeBucket = Enum.TryParse<AgeBucket>(Value(e), out var bucket) ? bucket : null,
        });

    private Task OnSortChanged(ChangeEventArgs e) =>
        ApplyAsync(_filter with
        {
            Sort = Enum.TryParse<SortOrder>(e.Value?.ToString(), out var sort) ? sort : SortOrder.OldestFirst,
        });

    private Task OnAgingOnlyChanged(ChangeEventArgs e) =>
        ApplyAsync(_filter with { AgingOnly = e.Value is true });

    private Task ToggleBucket(AgeBucket bucket) =>
        ApplyAsync(_filter with { AgeBucket = _filter.AgeBucket == bucket ? null : bucket });

    private void ShowAllAging() =>
        Navigation.NavigateTo($"/aging-review?dealership={Uri.EscapeDataString(BranchQuery.Encode(_filter.DealershipId))}");

    private Task ClearFilters() =>
        ApplyAsync(VehicleFilter.None with { DealershipId = _filter.DealershipId, Sort = _filter.Sort });

    private void Open(Vehicle summary) => _selected = summary;

    private void Close() => _selected = null;

    private async Task SaveAction(VehicleAction action)
    {
        if (_selected is not { } summary)
        {
            return;
        }

        _selected = null;
        await _workflow.SaveAsync(summary.Vin, summary.StockNumber, action);
        await LoadAsync();
    }

    private string DescribeFilter() => _filter.IsEmpty
        ? "none"
        : string.Join(
            ',',
            new[]
            {
                _filter.DealershipId is null ? null : "branch",
                _filter.Make is null ? null : "make",
                _filter.Model is null ? null : "model",
                _filter.AgeBucket is null ? null : "age",
                string.IsNullOrWhiteSpace(_filter.Search) ? null : "search",
                _filter.AgingOnly ? "aging" : null,
            }.Where(x => x is not null));

    public ValueTask DisposeAsync()
    {
        _workflow.Changed -= OnWorkflowChanged;
        _workflow.Dispose();
        return ValueTask.CompletedTask;
    }
}
