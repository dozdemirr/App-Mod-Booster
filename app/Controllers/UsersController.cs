using Microsoft.AspNetCore.Mvc;
using ExpenseMgmt.Models;
using ExpenseMgmt.Services;

namespace ExpenseMgmt.Controllers;

/// <summary>
/// Users API - retrieve user information.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public UsersController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all active users.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<User>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers()
    {
        var (users, error) = await _expenseService.GetUsersAsync();
        if (error != null)
            return Ok(new { data = users, warning = error, isDummyData = true });
        return Ok(new { data = users, warning = (string?)null, isDummyData = false });
    }
}
