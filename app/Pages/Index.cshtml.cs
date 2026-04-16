using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseMgmt.Models;
using ExpenseMgmt.Services;

namespace ExpenseMgmt.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public List<ExpenseSummary> Summary { get; set; } = new();
    public List<Expense> RecentExpenses { get; set; } = new();

    public IndexModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        var (summary, summaryError) = await _expenseService.GetExpenseSummaryAsync();
        Summary = summary;
        if (summaryError != null) ViewData["DbError"] = summaryError;

        var (expenses, expError) = await _expenseService.GetExpensesAsync();
        RecentExpenses = expenses.Take(5).ToList();
        if (expError != null && summaryError == null) ViewData["DbError"] = expError;
    }
}
