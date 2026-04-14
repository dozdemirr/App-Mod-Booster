using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly UserService _userService;

    public List<Expense> Expenses { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public List<ExpenseCategory> Categories { get; set; } = new();
    public string Filter { get; set; } = "all";

    public ExpensesModel(ExpenseService expenseService, UserService userService)
    {
        _expenseService = expenseService;
        _userService = userService;
    }

    public void OnGet(string filter = "all")
    {
        Filter = filter;
        string? error = null;

        if (filter == "all")
        {
            var (expenses, err) = _expenseService.GetAllExpenses();
            Expenses = expenses;
            error = err;
        }
        else
        {
            var (expenses, err) = _expenseService.GetExpensesByStatus(filter);
            Expenses = expenses;
            error = err;
        }

        var (users, usersErr) = _userService.GetAllUsers();
        Users = users;
        var (cats, catsErr) = _userService.GetAllCategories();
        Categories = cats;

        error ??= usersErr ?? catsErr;
        if (error != null) ViewData["DbError"] = error;
    }

    public IActionResult OnPostCreate(int userId, int categoryId, decimal amountGBP, DateTime expenseDate, string description, string? receiptFile)
    {
        var request = new CreateExpenseRequest
        {
            UserId = userId,
            CategoryId = categoryId,
            AmountGBP = amountGBP,
            Currency = "GBP",
            ExpenseDate = expenseDate,
            Description = description,
            ReceiptFile = receiptFile
        };
        var (success, error) = _expenseService.CreateExpense(request);
        if (error != null) TempData["DbError"] = error;
        return RedirectToPage();
    }

    public IActionResult OnPostSubmit(int id)
    {
        var (success, error) = _expenseService.SubmitExpense(id);
        if (error != null) TempData["DbError"] = error;
        return RedirectToPage();
    }

    public IActionResult OnPostApprove(int id, int reviewedBy)
    {
        var (success, error) = _expenseService.ApproveExpense(id, reviewedBy);
        if (error != null) TempData["DbError"] = error;
        return RedirectToPage();
    }

    public IActionResult OnPostReject(int id, int reviewedBy)
    {
        var (success, error) = _expenseService.RejectExpense(id, reviewedBy);
        if (error != null) TempData["DbError"] = error;
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int id)
    {
        var (success, error) = _expenseService.DeleteExpense(id);
        if (error != null) TempData["DbError"] = error;
        return RedirectToPage();
    }
}
