using Keyloop.Inventory.Application.Dtos;

namespace Keyloop.Inventory.Application.Interfaces;

public interface IBranchDirectory
{
    IReadOnlyList<Dealership> All { get; }

    Dealership Default { get; }
}
