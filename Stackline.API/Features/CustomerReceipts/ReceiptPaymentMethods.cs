namespace Stackline.API.Features.CustomerReceipts;

public static class ReceiptPaymentMethods
{
    public const string Cash = "Cash";
    public const string BankTransfer = "BankTransfer";
    public const string Card = "Card";

    public static bool IsValid(string? value) =>
        value is Cash or BankTransfer or Card;
}