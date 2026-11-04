using Keyloop.Inventory.Application.Dtos;
using Keyloop.Inventory.Application.Interfaces;

namespace Keyloop.Inventory.Infrastructure.Persistence;

public sealed class BranchDirectory : IBranchDirectory
{
    public IReadOnlyList<Dealership> All => SeedCatalogue.Default.Branches;

    public Dealership Default => All[0];
}
