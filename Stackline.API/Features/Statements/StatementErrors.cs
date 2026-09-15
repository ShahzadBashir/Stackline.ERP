using Stackline.API.Common;

namespace Stackline.API.Features.Statements;

public static class StatementErrors
{
    public static readonly Error InvalidDateRange = new(
        "statement.invalid_date_range",
        "Enter valid dates. From date cannot be later than To date.",
        ErrorType.Validation);

    public static readonly Error InvalidPagination = new(
        "statement.invalid_pagination",
        "Page must be positive and page size must be between 1 and 100.",
        ErrorType.Validation);

    public static readonly Error BalanceOutOfRange = new(
        "statement.balance_out_of_range",
        "The statement balance exceeds the supported numeric range.",
        ErrorType.Conflict);
}