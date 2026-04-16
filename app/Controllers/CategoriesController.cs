using Microsoft.AspNetCore.Mvc;
using ExpenseMgmt.Models;
using ExpenseMgmt.Services;

namespace ExpenseMgmt.Controllers;

/// <summary>
/// Categories API - retrieve expense categories.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public CategoriesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all active expense categories.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ExpenseCategory>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories()
    {
        var (categories, error) = await _expenseService.GetCategoriesAsync();
        if (error != null)
            return Ok(new { data = categories, warning = error, isDummyData = true });
        return Ok(new { data = categories, warning = (string?)null, isDummyData = false });
    }
}
