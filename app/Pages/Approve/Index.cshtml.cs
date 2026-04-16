using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseMgmt.Models;
using ExpenseMgmt.Services;

namespace ExpenseMgmt.Pages.Approve;

public class ApproveModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public List<Expense> PendingExpenses { get; set; } = new();
    public List<User> Managers { get; set; } = new();
    public string? Filter { get; set; }
    public int SelectedReviewerId { get; set; }
    public string? StatusMessage { get; set; }

    public ApproveModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync(string? filter = null, int reviewerId = 0)
    {
        Filter = filter;
        await LoadPageAsync(filter, reviewerId);
    }

    public async Task<IActionResult> OnPostApproveAsync(int expenseId, int reviewerId)
    {
        var (success, error) = await _expenseService.ApproveExpenseAsync(expenseId, reviewerId);
        if (error != null) ViewData["DbError"] = error;
        TempData["StatusMessage"] = success ? $"Expense #{expenseId} approved successfully." : "Could not approve expense.";
        return RedirectToPage(new { reviewerId });
    }

    public async Task<IActionResult> OnPostRejectAsync(int expenseId, int reviewerId, string? rejectionReason)
    {
        var (success, error) = await _expenseService.RejectExpenseAsync(expenseId, reviewerId, rejectionReason);
        if (error != null) ViewData["DbError"] = error;
        TempData["StatusMessage"] = success ? $"Expense #{expenseId} rejected." : "Could not reject expense.";
        return RedirectToPage(new { reviewerId });
    }

    private async Task LoadPageAsync(string? filter, int reviewerId)
    {
        var (pending, pendingError) = await _expenseService.GetPendingExpensesAsync(filter);
        PendingExpenses = pending;
        if (pendingError != null) ViewData["DbError"] = pendingError;

        var (users, _) = await _expenseService.GetUsersAsync();
        Managers = users.Where(u => u.RoleName == "Manager").ToList();
        if (!Managers.Any()) Managers = users; // fallback: show all users

        SelectedReviewerId = reviewerId > 0 ? reviewerId : (Managers.FirstOrDefault()?.UserId ?? 0);
        StatusMessage = TempData["StatusMessage"]?.ToString();
    }
}
