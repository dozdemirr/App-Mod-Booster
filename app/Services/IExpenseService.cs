using ExpenseMgmt.Models;

namespace ExpenseMgmt.Services;

public interface IExpenseService
{
    Task<(List<Expense> Expenses, string? ErrorMessage)> GetExpensesAsync(string? filter = null, int? userId = null);
    Task<(Expense? Expense, string? ErrorMessage)> GetExpenseByIdAsync(int expenseId);
    Task<(int ExpenseId, string? ErrorMessage)> CreateExpenseAsync(CreateExpenseRequest request);
    Task<(bool Success, string? ErrorMessage)> SubmitExpenseAsync(int expenseId, int userId);
    Task<(bool Success, string? ErrorMessage)> ApproveExpenseAsync(int expenseId, int reviewerId);
    Task<(bool Success, string? ErrorMessage)> RejectExpenseAsync(int expenseId, int reviewerId, string? rejectionReason);
    Task<(List<Expense> Expenses, string? ErrorMessage)> GetPendingExpensesAsync(string? filter = null);
    Task<(List<ExpenseCategory> Categories, string? ErrorMessage)> GetCategoriesAsync();
    Task<(List<User> Users, string? ErrorMessage)> GetUsersAsync();
    Task<(List<Expense> Expenses, string? ErrorMessage)> GetExpensesByUserIdAsync(int userId, string? filter = null);
    Task<(bool Success, string? ErrorMessage)> DeleteExpenseAsync(int expenseId, int userId);
    Task<(List<ExpenseSummary> Summary, string? ErrorMessage)> GetExpenseSummaryAsync(int? userId = null);
}
