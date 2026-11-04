namespace Keyloop.Inventory.Domain.Enums;

public enum ActionStatus
{
    PriceReductionPlanned = 1,
    MarketingPromotion = 2,
    TransferToBranch = 3,
    WholesaleOrAuction = 4,
    Reconditioning = 5,
    HoldForCustomerDeposit = 6,
}

public static class ActionStatuses
{
    public static string Label(this ActionStatus status) => status switch
    {
        ActionStatus.PriceReductionPlanned => "Price reduction planned",
        ActionStatus.MarketingPromotion => "Marketing promotion",
        ActionStatus.TransferToBranch => "Transfer to another branch",
        ActionStatus.WholesaleOrAuction => "Wholesale / auction",
        ActionStatus.Reconditioning => "Reconditioning / detail",
        ActionStatus.HoldForCustomerDeposit => "Hold – customer deposit",
        _ => status.ToString(),
    };
}
