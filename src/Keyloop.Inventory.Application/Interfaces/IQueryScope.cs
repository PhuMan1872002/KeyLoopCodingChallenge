namespace Keyloop.Inventory.Application.Interfaces;

public interface IQueryScope : IDisposable
{
    void SetResultCount(int count);
}
