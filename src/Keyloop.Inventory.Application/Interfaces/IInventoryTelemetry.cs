namespace Keyloop.Inventory.Application.Interfaces;

public interface IInventoryTelemetry
{
    IQueryScope BeginQuery(string filterShape);

    void RecordActionLogged(string status);

    void RecordActionFailed(string reason);
}
