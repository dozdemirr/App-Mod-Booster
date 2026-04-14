using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class CategoriesModel : PageModel
{
    private readonly UserService _userService;

    public List<ExpenseCategory> Categories { get; set; } = new();

    public CategoriesModel(UserService userService)
    {
        _userService = userService;
    }

    public void OnGet()
    {
        var (categories, error) = _userService.GetAllCategories();
        Categories = categories;
        if (error != null) ViewData["DbError"] = error;
    }
}
