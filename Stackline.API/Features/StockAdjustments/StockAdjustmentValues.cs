namespace Stackline.API.Features.StockAdjustments;

public static class StockAdjustmentStatuses
{
    public const string Draft = "Draft";
    public const string Posted = "Posted";
}

public static class StockAdjustmentDirections
{
    public const string Increase = "Increase";
    public const string Decrease = "Decrease";

    public static bool IsValid(string? direction)
    {
        return direction is Increase or Decrease;
    }
}

public static class StockAdjustmentReasons
{
    public const string OpeningStock = "OpeningStock";
    public const string DamagedGoods = "DamagedGoods";
    public const string MissingStock = "MissingStock";
    public const string StockCountCorrection = "StockCountCorrection";
    public const string Other = "Other";

    public static bool IsValid(string? reason)
    {
        return reason is
            OpeningStock or
            DamagedGoods or
            MissingStock or
            StockCountCorrection or
            Other;
    }

    public static bool AllowsDirection(string? reason, string? direction)
    {
        if (!IsValid(reason) ||
            !StockAdjustmentDirections.IsValid(direction))
        {
            return false;
        }

        return reason switch
        {
            OpeningStock =>
                direction == StockAdjustmentDirections.Increase,

            DamagedGoods or MissingStock =>
                direction == StockAdjustmentDirections.Decrease,

            StockCountCorrection or Other => true,

            _ => false
        };
    }
}