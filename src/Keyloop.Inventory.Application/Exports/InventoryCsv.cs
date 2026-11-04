using System.Globalization;
using System.Text;
using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;

namespace Keyloop.Inventory.Application.Exports;

public static class InventoryCsv
{
    private const string Header =
        "Stock number,VIN,Year,Make,Model,Trim,Colour,Location,List price,Stocked on," +
        "Days in stock,Age band,Action status,Proposed price,Review by,Note";

    public static string Render(IReadOnlyList<Vehicle> vehicles, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(vehicles);

        var csv = new StringBuilder().AppendLine(Header);

        foreach (var v in vehicles)
        {
            var action = v.CurrentAction;

            csv.AppendLine(string.Join(',',
            [
                Field(v.StockNumber),
                Field(v.Vin),
                Number(v.ModelYear),
                Field(v.Make),
                Field(v.Model),
                Field(v.Trim),
                Field(v.Colour),
                Field(v.Location),
                Money(v.ListPrice),
                Day(v.StockedOn),
                Number(v.DaysInStock(today)),
                Field(v.AgeBandOn(today).Label()),
                Field(action?.StatusLabel ?? string.Empty),
                action?.ProposedPrice is { } price ? Money(price) : string.Empty,
                action?.TargetDate is { } due ? Day(due) : string.Empty,
                Field(action?.Note ?? string.Empty),
            ]));
        }

        return csv.ToString();
    }

    public static string Field(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Money(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Day(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
