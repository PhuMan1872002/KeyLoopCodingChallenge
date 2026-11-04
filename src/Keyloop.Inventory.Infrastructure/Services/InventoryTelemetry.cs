using System.Diagnostics;
using System.Diagnostics.Metrics;
using Keyloop.Inventory.Application.Interfaces;

namespace Keyloop.Inventory.Infrastructure.Services;

public sealed class InventoryTelemetry : IInventoryTelemetry, IDisposable
{
    public const string MeterName = "Keyloop.Inventory";

    public static readonly ActivitySource ActivitySource = new(MeterName);

    private readonly Meter _meter = new(MeterName);
    private readonly Histogram<double> _queryDuration;
    private readonly Counter<long> _actionsLogged;
    private readonly Counter<long> _actionFailures;

    public InventoryTelemetry()
    {
        _queryDuration = _meter.CreateHistogram<double>(
            "inventory.query.duration",
            unit: "ms",
            description: "Time to fetch, filter and project the inventory for a dashboard render.");

        _actionsLogged = _meter.CreateCounter<long>(
            "inventory.actions.logged",
            description: "Actions successfully recorded against a vehicle, by status.");

        _actionFailures = _meter.CreateCounter<long>(
            "inventory.actions.failed",
            description: "Attempts to record an action that did not persist.");
    }

    public IQueryScope BeginQuery(string filterShape) => new QueryScope(this, filterShape);

    public void RecordActionLogged(string status) =>
        _actionsLogged.Add(1, new KeyValuePair<string, object?>("action.status", status));

    public void RecordActionFailed(string reason) =>
        _actionFailures.Add(1, new KeyValuePair<string, object?>("failure.reason", reason));

    public void Dispose() => _meter.Dispose();

    private sealed class QueryScope : IQueryScope
    {
        private readonly InventoryTelemetry _owner;
        private readonly Activity? _activity;
        private readonly long _startedAt;

        internal QueryScope(InventoryTelemetry owner, string filterShape)
        {
            _owner = owner;
            _startedAt = Stopwatch.GetTimestamp();
            _activity = ActivitySource.StartActivity("inventory.query");
            _activity?.SetTag("inventory.filter", filterShape);
        }

        public void SetResultCount(int count) => _activity?.SetTag("inventory.result_count", count);

        public void Dispose()
        {
            _owner._queryDuration.Record(Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds);
            _activity?.Dispose();
        }
    }
}
