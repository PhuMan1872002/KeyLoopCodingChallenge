using Keyloop.Inventory.Application.Exceptions;
using Keyloop.Inventory.Application.Interfaces;
using Keyloop.Inventory.Application.Queries;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;
using Keyloop.Inventory.Infrastructure.Persistence;

namespace Keyloop.Inventory.Tests.Domain;

internal static class TestData
{
    public static readonly DateOnly Today = new(2026, 11, 2);

    public static readonly IBranchDirectory Branches = new BranchDirectory();

    public static readonly DateTimeOffset Sometime = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset At(int year, int month, int day) =>
        new(year, month, day, 9, 0, 0, TimeSpan.Zero);

    public static Vehicle Vehicle(
        string vin,
        string make = "Ford",
        string model = "Focus",
        int daysInStock = 10,
        decimal listPrice = 15_000m,
        IReadOnlyList<VehicleAction>? actions = null,
        string? dealershipId = null) => new()
        {
            Vin = vin,
            StockNumber = "ST-" + vin,
            Make = make,
            Model = model,
            Trim = "Titanium",
            ModelYear = 2023,
            Mileage = 20_000,
            ListPrice = listPrice,
            Colour = "Midnight Black",
            Location = "Lot A · 01",
            StockedOn = Today.AddDays(-daysInStock),
            DealershipId = dealershipId ?? Branches.Default.Id,
            Actions = actions ?? [],
        };

    public static Vehicle Aging(
        string vin,
        int days = 120,
        IReadOnlyList<VehicleAction>? actions = null,
        decimal listPrice = 30_000m) =>
        Vehicle(vin, make: "Toyota", model: "Camry", daysInStock: days, listPrice: listPrice, actions: actions);

    public static VehicleAction Action(
        ActionStatus status = ActionStatus.PriceReductionPlanned,
        DateTimeOffset? recordedAt = null,
        string? note = null) => new()
    {
        Status = status,
        RecordedAt = recordedAt ?? Sometime,
        Note = note,
    };

    public static InventoryQueryService Service(params Vehicle[] vehicles) =>
        new(new FakeInventoryClient(vehicles));

    public sealed class FakeInventoryClient(params Vehicle[] vehicles) : IInventoryClient
    {
        private readonly List<Vehicle> _vehicles = [.. vehicles];

        public Task<IReadOnlyList<Vehicle>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Vehicle>>(_vehicles);

        public Task AppendActionAsync(string vin, VehicleAction action, CancellationToken cancellationToken = default)
        {
            var index = _vehicles.FindIndex(v => v.Vin == vin);
            if (index < 0)
            {
                throw new VehicleNotFoundException(vin);
            }

            _vehicles[index] = _vehicles[index] with { Actions = [.. _vehicles[index].Actions, action] };
            return Task.CompletedTask;
        }
    }
}
