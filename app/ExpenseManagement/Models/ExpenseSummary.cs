namespace ExpenseManagement.Models;

public class ExpenseSummary
{
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmountGBP { get; set; }
}
