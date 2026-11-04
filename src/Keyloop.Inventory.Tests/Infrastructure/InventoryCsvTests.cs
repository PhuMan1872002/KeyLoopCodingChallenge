using Keyloop.Inventory.Domain.Entities;
using Keyloop.Inventory.Domain.Enums;
using Keyloop.Inventory.Application.Exports;
using Keyloop.Inventory.Tests.Domain;

namespace Keyloop.Inventory.Tests.Infrastructure;

public class InventoryCsvTests
{
    private static readonly DateOnly Today = TestData.Today;

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1")]
    [InlineData("-1+1")]
    [InlineData("@SUM(A1)")]
    [InlineData("=HYPERLINK(\"http://example.test\",\"click\")")]
    public void A_cell_that_a_spreadsheet_would_read_as_a_formula_is_neutralised(string note)
    {
        var escaped = InventoryCsv.Field(note);

        Assert.StartsWith("'", escaped.TrimStart('"'));
    }

    [Theory]
    [InlineData("Reduce to clear", "Reduce to clear")]
    [InlineData("Lot A", "Lot A")]
    [InlineData("2024 Toyota Camry", "2024 Toyota Camry")]
    public void Ordinary_text_is_left_exactly_as_it_was(string value, string expected)
    {
        Assert.Equal(expected, InventoryCsv.Field(value));
    }

    [Fact]
    public void A_cell_containing_a_comma_or_a_quote_is_still_quoted_and_doubled()
    {
        Assert.Equal("\"Lot A, bay 2\"", InventoryCsv.Field("Lot A, bay 2"));
        Assert.Equal("\"He said \"\"sell it\"\"\"", InventoryCsv.Field("He said \"sell it\""));
    }

    [Fact]
    public void A_formula_containing_a_comma_gets_both_treatments()
    {
        Assert.Equal("\"'=SUM(1,2)\"", InventoryCsv.Field("=SUM(1,2)"));
    }

    [Fact]
    public void The_export_renders_a_header_and_one_row_per_vehicle()
    {
        var planned = TestData.Action(ActionStatus.PriceReductionPlanned);

        var rows = InventoryCsv
            .Render([TestData.Vehicle("VIN-00000001", daysInStock: 120, actions: [planned])], Today)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("Stock number,VIN,Year", rows[0]);
        Assert.Contains("VIN-00000001", rows[1]);
        Assert.Contains("Price reduction planned", rows[1]);
        Assert.Contains("120", rows[1]);
    }

    [Fact]
    public void A_vehicle_with_no_logged_decision_leaves_those_columns_empty_rather_than_absent()
    {
        var header = InventoryCsv.Render([], Today).Split('\n')[0];
        var row = InventoryCsv
            .Render([TestData.Vehicle("VIN-00000002", daysInStock: 10)], Today)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)[1];

        Assert.Equal(header.Split(',').Length, row.Split(',').Length);
    }
}
