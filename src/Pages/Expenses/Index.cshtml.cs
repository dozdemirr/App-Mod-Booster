using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Expenses;

public class IndexModel : PageModel
{
    private readonly ExpenseService _svc;
    public IndexModel(ExpenseService svc) { _svc = svc; }
    public List<Expense> Expenses { get; set; } = new();
    public List<ExpenseStatus> Statuses { get; set; } = new();
    public List<ExpenseUser> Users { get; set; } = new();
    public string? StatusFilter { get; set; }
    public int? UserFilter { get; set; }

    public async Task OnGetAsync(string? status, int? userId)
    {
        StatusFilter = status; UserFilter = userId;
        Statuses = await _svc.GetAllStatusesAsync();
        Users = await _svc.GetAllUsersAsync();
        Expenses = !string.IsNullOrEmpty(status) ? await _svc.GetExpensesByStatusAsync(status)
            : userId.HasValue ? await _svc.GetExpensesByUserAsync(userId.Value)
            : await _svc.GetAllExpensesAsync();
        if (_svc.LastError != null) { ViewData["DbError"] = _svc.LastError; ViewData["DbErrorFile"] = _svc.LastErrorFile; ViewData["DbErrorLine"] = _svc.LastErrorLine; }
    }
}
