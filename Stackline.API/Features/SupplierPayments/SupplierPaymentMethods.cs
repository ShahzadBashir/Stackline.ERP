namespace Stackline.API.Features.SupplierPayments;

public static class SupplierPaymentMethods
{
    public const string Cash = "Cash";
    public const string BankTransfer = "BankTransfer";
    public const string Card = "Card";

    public static bool IsValid(string? value) =>
        value is Cash or BankTransfer or Card;
}