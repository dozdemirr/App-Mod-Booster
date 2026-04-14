using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly UserService _userService;

    public CategoriesController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var (categories, error) = _userService.GetAllCategories();
        return Ok(new { data = categories, error });
    }
}
