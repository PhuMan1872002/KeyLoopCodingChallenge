using Keyloop.Inventory.Application.Dtos;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;

namespace Keyloop.Inventory.Infrastructure.Persistence;

public static class InventorySeed
{
    private const int RandomSeed = 20261102;
    private const string VinAlphabet = "ABCDEFGHJKLMNPRSTUVWXYZ0123456789";

    public static IReadOnlyList<Vehicle> Generate(int count, DateOnly today) =>
        Generate(count, today, SeedCatalogue.Default);

    public static IReadOnlyList<Vehicle> Generate(int count, DateOnly today, SeedCatalogue catalogue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentNullException.ThrowIfNull(catalogue);

        var random = new Random(RandomSeed);
        var vehicles = new List<Vehicle>(count);
        var stockSequence = 24_500;

        for (var i = 0; i < count; i++)
        {
            var make = catalogue.Makes[random.Next(catalogue.Makes.Length)];
            var model = make.Models[random.Next(make.Models.Length)];
            var daysInStock = DaysInStock(random);
            var stockedOn = today.AddDays(-daysInStock);
            var listPrice = ToNearestFifty(random.Next(model.LowPrice, model.HighPrice));

            stockSequence += random.Next(1, 4);

            vehicles.Add(new Vehicle
            {
                Vin = GenerateVin(random),
                StockNumber = $"{make.Code}-{stockSequence}",
                Make = make.Make,
                Model = model.Model,
                Trim = model.Trims[random.Next(model.Trims.Length)],
                ModelYear = today.Year - random.Next(0, 3),
                Mileage = daysInStock < 40 ? random.Next(5, 600) : random.Next(600, 48_000),
                ListPrice = listPrice,
                Colour = catalogue.Colours[random.Next(catalogue.Colours.Length)],
                Location = GenerateLocation(random, daysInStock, catalogue.Lots),
                StockedOn = stockedOn,
                DealershipId = PickBranch(random, catalogue.Branches).Id,
                Actions = PriorActionsFor(random, stockedOn, today, listPrice, catalogue.PriorDecisions),
            });
        }

        return vehicles;
    }

    private static Dealership PickBranch(Random random, Dealership[] branches)
    {
        var roll = random.Next(branches.Sum(b => b.Share));

        foreach (var branch in branches)
        {
            roll -= branch.Share;
            if (roll < 0)
            {
                return branch;
            }
        }

        return branches[^1];
    }

    private static int DaysInStock(Random random) => random.Next(100) switch
    {
        < 55 => random.Next(0, 76),
        < 78 => random.Next(76, 91),
        < 94 => random.Next(91, 151),
        _ => random.Next(151, 420),
    };

    private static string GenerateLocation(Random random, int daysInStock, string[] lots)
    {
        if (daysInStock < 30 && random.Next(100) < 35)
        {
            return "Showroom";
        }

        var lot = daysInStock > 180 && random.Next(100) < 45 ? "Overflow" : lots[random.Next(lots.Length)];
        return $"{lot} · {random.Next(1, 40):00}";
    }

    private static IReadOnlyList<VehicleAction> PriorActionsFor(
        Random random,
        DateOnly stockedOn,
        DateOnly today,
        decimal listPrice,
        SeedDecision[] decisions)
    {
        if (!AgingPolicy.IsAging(stockedOn, today) || random.Next(100) >= 40)
        {
            return [];
        }

        var decision = decisions[random.Next(decisions.Length)];

        var daysSinceFlagged = AgingPolicy.DaysInStock(stockedOn, today) - AgingPolicy.AgingThresholdDays;
        var loggedOn = today.AddDays(-random.Next(0, Math.Max(1, daysSinceFlagged)));

        return
        [
            new VehicleAction
            {
                Status = decision.Status,
                Note = decision.Note,
                ProposedPrice = decision.Status == ActionStatus.PriceReductionPlanned
                    ? ToNearestFifty(listPrice * 0.94m)
                    : null,
                TargetDate = loggedOn.AddDays(random.Next(14, 22)),
                RecordedAt = new DateTimeOffset(loggedOn.ToDateTime(new TimeOnly(9, 30)), TimeSpan.Zero),
            },
        ];
    }

    private static decimal ToNearestFifty(decimal value) => Math.Round(value / 50m) * 50m;

    private static string GenerateVin(Random random) =>
        string.Create(17, random, static (span, rng) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = VinAlphabet[rng.Next(VinAlphabet.Length)];
            }
        });
}
