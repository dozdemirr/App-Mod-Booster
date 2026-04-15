namespace AppModAssist.Models;

public sealed record ExpenseItem(
    int ExpenseId,
    int UserId,
    string UserName,
    int CategoryId,
    string CategoryName,
    int StatusId,
    string StatusName,
    int AmountMinor,
    string Currency,
    DateOnly ExpenseDate,
    string? Description,
    DateTime? SubmittedAt,
    DateTime CreatedAt);

public sealed record ExpenseFilter(string? Status, int? UserId, int? CategoryId);

public sealed record CreateExpenseRequest(
    int UserId,
    int CategoryId,
    int AmountMinor,
    DateOnly ExpenseDate,
    string? Description);

public sealed record UpdateExpenseStatusRequest(
    int ExpenseId,
    string NewStatus,
    int ReviewedByUserId);

public sealed record LookupItem(int Id, string Name);

public sealed record ChatRequest(string Message);

public sealed record ChatResponse(string Reply, bool UsedGenAI);

public sealed record UiErrorDetails(string Message, DateTimeOffset Timestamp);
