using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseMgmt.Models;
using ExpenseMgmt.Services;

namespace ExpenseMgmt.Pages.Expenses;

public class ExpensesIndexModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public List<Expense> Expenses { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public string? Filter { get; set; }
    public int? SelectedUserId { get; set; }
    public string? StatusMessage { get; set; }

    public ExpensesIndexModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync(string? filter = null, int? userId = null)
    {
        Filter = filter;
        SelectedUserId = userId;

        var (expenses, expError) = await _expenseService.GetExpensesAsync(filter, userId);
        Expenses = expenses;
        if (expError != null) ViewData["DbError"] = expError;

        var (users, userError) = await _expenseService.GetUsersAsync();
        Users = users;
    }

    public async Task<IActionResult> OnPostSubmitAsync(int expenseId, int userId)
    {
        var (success, error) = await _expenseService.SubmitExpenseAsync(expenseId, userId);
        if (error != null) ViewData["DbError"] = error;
        TempData["StatusMessage"] = success ? "Expense submitted for approval." : "Could not submit expense.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int expenseId, int userId)
    {
        var (success, error) = await _expenseService.DeleteExpenseAsync(expenseId, userId);
        if (error != null) ViewData["DbError"] = error;
        TempData["StatusMessage"] = success ? "Draft expense deleted." : "Could not delete expense.";
        return RedirectToPage();
    }
}
