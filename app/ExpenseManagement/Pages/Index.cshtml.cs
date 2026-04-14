using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly ExpenseService _expenseService;

    public List<ExpenseSummary> Summary { get; set; } = new();
    public List<Expense> RecentExpenses { get; set; } = new();

    public IndexModel(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public void OnGet()
    {
        var (summary, summaryError) = _expenseService.GetExpenseSummary();
        Summary = summary;

        var (expenses, expensesError) = _expenseService.GetAllExpenses();
        RecentExpenses = expenses.OrderByDescending(e => e.CreatedAt).Take(10).ToList();

        var error = summaryError ?? expensesError;
        if (error != null) ViewData["DbError"] = error;
    }
}
