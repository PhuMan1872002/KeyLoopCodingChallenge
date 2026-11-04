namespace Keyloop.Inventory.Web.ViewModels;

public static class BranchQuery
{
    public const string All = "all";

    public static string Encode(string? dealershipId) => dealershipId ?? All;

    public static string? Decode(string? query) => query is null or All ? null : query;
}
