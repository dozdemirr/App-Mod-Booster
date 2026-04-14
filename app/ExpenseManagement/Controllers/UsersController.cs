using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var (users, error) = _userService.GetAllUsers();
        return Ok(new { data = users, error });
    }

    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        var (user, error) = _userService.GetUserById(id);
        if (user == null) return NotFound();
        return Ok(new { data = user, error });
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateUserRequest request)
    {
        var (success, error) = _userService.CreateUser(request);
        if (!success) return BadRequest(new { error });
        return Ok(new { success = true });
    }
}
