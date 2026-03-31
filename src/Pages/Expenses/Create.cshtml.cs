using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Pages.Expenses;

public class CreateModel : PageModel
{
    private readonly ExpenseService _svc;
    public CreateModel(ExpenseService svc) { _svc = svc; }
    [BindProperty] public CreateExpenseRequest Input { get; set; } = new() { ExpenseDate = DateTime.Today, Currency = "GBP" };
    public List<SelectListItem> UserItems { get; set; } = new();
    public List<SelectListItem> CategoryItems { get; set; } = new();

    public async Task OnGetAsync()
    {
        var users = await _svc.GetAllUsersAsync();
        var cats = await _svc.GetAllCategoriesAsync();
        UserItems = users.Select(u => new SelectListItem(u.UserName, u.UserId.ToString())).ToList();
        CategoryItems = cats.Select(c => new SelectListItem(c.CategoryName, c.CategoryId.ToString())).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { await OnGetAsync(); return Page(); }
        var id = await _svc.CreateExpenseAsync(Input);
        if (id == null) { ModelState.AddModelError("", _svc.LastError ?? "Failed to create expense"); await OnGetAsync(); return Page(); }
        return RedirectToPage("/Expenses/Detail", new { id });
    }
}
