namespace AppModAssist.Models;

public record ExpenseItem(
    int ExpenseId,
    string UserName,
    string UserEmail,
    string CategoryName,
    string StatusName,
    decimal AmountGbp,
    DateOnly ExpenseDate,
    string? Description,
    string? ReceiptFile,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt);

public record CreateExpenseRequest(
    int UserId,
    int CategoryId,
    decimal AmountGbp,
    DateOnly ExpenseDate,
    string? Description,
    string? ReceiptFile);

public record ReviewExpenseRequest(
    int ExpenseId,
    int ReviewedByUserId,
    bool Approve,
    string? Notes);

public record SubmitExpenseRequest(int ExpenseId);

public record LookupItem(int Id, string Name);

public record DashboardData(
    IReadOnlyList<ExpenseItem> Expenses,
    IReadOnlyList<LookupItem> Users,
    IReadOnlyList<LookupItem> Categories,
    IReadOnlyList<LookupItem> Statuses);
