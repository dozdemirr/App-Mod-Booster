using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Pages;

public class IndexModel : PageModel
{
    private readonly ExpenseService _expenseService;

    public IndexModel(ExpenseService expenseService) { _expenseService = expenseService; }

    public int TotalExpenses { get; set; }
    public int SubmittedExpenses { get; set; }
    public int ApprovedExpenses { get; set; }
    public decimal TotalAmountGBP { get; set; }
    public List<Expense> RecentExpenses { get; set; } = new();

    public async Task OnGetAsync()
    {
        var expenses = await _expenseService.GetAllExpensesAsync();
        if (_expenseService.LastError != null) { ViewData["DbError"] = _expenseService.LastError; ViewData["DbErrorFile"] = _expenseService.LastErrorFile; ViewData["DbErrorLine"] = _expenseService.LastErrorLine; }
        TotalExpenses = expenses.Count;
        SubmittedExpenses = expenses.Count(e => e.StatusName == "Submitted");
        ApprovedExpenses = expenses.Count(e => e.StatusName == "Approved");
        TotalAmountGBP = expenses.Sum(e => e.AmountGBP);
        RecentExpenses = expenses.Take(10).ToList();
    }
}
