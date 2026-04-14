namespace ModernExpenseApp.Models;

public sealed class ExpenseDto
{
    public int ExpenseId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int AmountMinor { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateOnly ExpenseDate { get; set; }
    public string? Description { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public sealed class UserDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
}

public sealed class CategoryDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

public sealed class StatusDto
{
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
}

public sealed class CreateExpenseRequest
{
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public int AmountMinor { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? Description { get; set; }
}

public sealed class ReviewExpenseRequest
{
    public int ManagerUserId { get; set; }
    public bool Approve { get; set; }
}

public sealed class ChatRequest
{
    public string Message { get; set; } = string.Empty;
}
