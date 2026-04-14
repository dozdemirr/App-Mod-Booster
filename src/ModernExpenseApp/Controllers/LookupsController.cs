using Microsoft.AspNetCore.Mvc;
using ModernExpenseApp.Services;

namespace ModernExpenseApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LookupsController(IExpenseService expenseService) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken) => Ok(await expenseService.GetUsersAsync(cancellationToken));

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken) => Ok(await expenseService.GetCategoriesAsync(cancellationToken));

    [HttpGet("statuses")]
    public async Task<IActionResult> GetStatuses(CancellationToken cancellationToken) => Ok(await expenseService.GetStatusesAsync(cancellationToken));
}
