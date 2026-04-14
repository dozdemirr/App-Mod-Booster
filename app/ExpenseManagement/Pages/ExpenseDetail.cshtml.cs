using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ExpenseDetailModel : PageModel
{
    private readonly ExpenseService _expenseService;

    public Expense? Expense { get; set; }

    public ExpenseDetailModel(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public void OnGet(int id)
    {
        var (expense, error) = _expenseService.GetExpenseById(id);
        Expense = expense;
        if (error != null) ViewData["DbError"] = error;
    }

    public IActionResult OnPostSubmit(int id)
    {
        var (success, error) = _expenseService.SubmitExpense(id);
        if (error != null) TempData["DbError"] = error;
        return RedirectToPage(new { id });
    }

    public IActionResult OnPostApprove(int id, int reviewedBy)
    {
        var (success, error) = _expenseService.ApproveExpense(id, reviewedBy);
        if (error != null) TempData["DbError"] = error;
        return RedirectToPage(new { id });
    }

    public IActionResult OnPostReject(int id, int reviewedBy)
    {
        var (success, error) = _expenseService.RejectExpense(id, reviewedBy);
        if (error != null) TempData["DbError"] = error;
        return RedirectToPage(new { id });
    }

    public IActionResult OnPostDelete(int id)
    {
        var (success, error) = _expenseService.DeleteExpense(id);
        if (error != null) TempData["DbError"] = error;
        return RedirectToPage("/Expenses");
    }
}
