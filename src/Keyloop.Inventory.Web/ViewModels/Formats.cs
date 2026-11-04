using System.Globalization;

namespace Keyloop.Inventory.Web.ViewModels;

public static class Formats
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    public static string Money(decimal value) => value.ToString("C0", Culture);

    public static string MoneyCompact(decimal value) => value switch
    {
        >= 1_000_000m => (value / 1_000_000m).ToString("C1", Culture) + "m",
        >= 1_000m => (value / 1_000m).ToString("C0", Culture) + "k",
        _ => Money(value),
    };

    public static string Date(DateOnly value) => value.ToString("d MMM yyyy", Culture);

    public static string Date(DateTimeOffset value) => value.ToString("d MMM yyyy", Culture);

    public static string DateTime(DateTimeOffset value) => value.ToString("d MMM yyyy, HH:mm", Culture) + " UTC";
}
