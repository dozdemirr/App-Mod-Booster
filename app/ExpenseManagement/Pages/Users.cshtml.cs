using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class UsersModel : PageModel
{
    private readonly UserService _userService;

    public List<User> Users { get; set; } = new();

    public UsersModel(UserService userService)
    {
        _userService = userService;
    }

    public void OnGet()
    {
        var (users, error) = _userService.GetAllUsers();
        Users = users;
        if (error != null) ViewData["DbError"] = error;
    }

    public IActionResult OnPostCreate(string userName, string email, int roleId, int? managerId)
    {
        var request = new CreateUserRequest
        {
            UserName = userName,
            Email = email,
            RoleId = roleId,
            ManagerId = managerId
        };
        var (success, error) = _userService.CreateUser(request);
        if (error != null) TempData["DbError"] = error;
        return RedirectToPage();
    }
}
