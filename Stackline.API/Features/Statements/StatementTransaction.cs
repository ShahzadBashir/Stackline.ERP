namespace Stackline.API.Features.Statements;

public sealed class StatementTransaction
{
    public DateOnly TransactionDate { get; set; }

    public DateTime PostedAt { get; set; }

    public string ReferenceType { get; set; } = string.Empty;

    public Guid ReferenceId { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Increase { get; set; }

    public decimal Decrease { get; set; }
}