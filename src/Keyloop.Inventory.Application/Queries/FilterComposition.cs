namespace Keyloop.Inventory.Application.Queries;

internal static class FilterComposition
{
    internal static IEnumerable<T> WhereIf<T>(
        this IEnumerable<T> source,
        bool condition,
        Func<T, bool> predicate) => condition ? source.Where(predicate) : source;
}
