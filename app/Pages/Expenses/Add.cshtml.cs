using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseMgmt.Models;
using ExpenseMgmt.Services;

namespace ExpenseMgmt.Pages.Expenses;

public class AddModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public List<ExpenseCategory> Categories { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int UserId { get; set; }
        public int CategoryId { get; set; }
        public decimal AmountGBP { get; set; }
        public DateTime ExpenseDate { get; set; } = DateTime.Today;
        public string? Description { get; set; }
    }

    public AddModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        await LoadFormDataAsync();
    }

    public async Task<IActionResult> OnPostAsync(int userId, int categoryId, decimal amountGBP, DateTime expenseDate, string? description)
    {
        await LoadFormDataAsync();

        if (userId <= 0 || categoryId <= 0 || amountGBP <= 0)
        {
            ErrorMessage = "Please fill in all required fields with valid values.";
            return Page();
        }

        var request = new CreateExpenseRequest
        {
            UserId = userId,
            CategoryId = categoryId,
            AmountGBP = amountGBP,
            ExpenseDate = expenseDate,
            Description = description
        };

        var (expenseId, error) = await _expenseService.CreateExpenseAsync(request);
        if (error != null)
        {
            ViewData["DbError"] = error;
            ErrorMessage = "There was a problem saving your expense. Please try again.";
            return Page();
        }

        TempData["StatusMessage"] = $"Expense saved as Draft (ID: {expenseId}). You can submit it for approval from My Expenses.";
        return RedirectToPage("/Expenses/Index");
    }

    private async Task LoadFormDataAsync()
    {
        var (categories, catError) = await _expenseService.GetCategoriesAsync();
        Categories = categories;
        if (catError != null) ViewData["DbError"] = catError;

        var (users, userError) = await _expenseService.GetUsersAsync();
        Users = users;
    }
}
