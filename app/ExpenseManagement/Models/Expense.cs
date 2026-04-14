namespace ExpenseManagement.Models;

public class Expense
{
    public int ExpenseId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public decimal AmountGBP { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReceiptFile { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public int StatusId { get; set; }
}

public class CreateExpenseRequest
{
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public decimal AmountGBP { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReceiptFile { get; set; }
}

public class UpdateExpenseRequest
{
    public int CategoryId { get; set; }
    public decimal AmountGBP { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReceiptFile { get; set; }
}
