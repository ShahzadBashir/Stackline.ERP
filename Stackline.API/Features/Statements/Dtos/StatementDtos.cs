namespace Stackline.API.Features.Statements.Dtos;

public sealed record StatementEntryResponse(
    DateOnly TransactionDate,
    DateTime PostedAt,
    string ReferenceType,
    Guid ReferenceId,
    string ReferenceNumber,
    string Description,
    decimal Increase,
    decimal Decrease,
    decimal RunningBalance);

public sealed record AccountStatementResponse(
    Guid AccountId,
    string AccountName,
    string AccountType,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal OpeningBalance,
    decimal TotalIncrease,
    decimal TotalDecrease,
    decimal ClosingBalance,
    int TotalCount,
    int Page,
    int PageSize,
    List<StatementEntryResponse> Entries);