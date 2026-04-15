using AppModAssist.Models;
using AppModAssist.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppModAssist.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LookupController(IExpenseService expenseService) : ControllerBase
{
    [HttpGet("categories")]
    public Task<IReadOnlyList<LookupItem>> GetCategories(CancellationToken cancellationToken) => expenseService.GetCategoriesAsync(cancellationToken);

    [HttpGet("statuses")]
    public Task<IReadOnlyList<LookupItem>> GetStatuses(CancellationToken cancellationToken) => expenseService.GetStatusesAsync(cancellationToken);

    [HttpGet("users")]
    public Task<IReadOnlyList<LookupItem>> GetUsers(CancellationToken cancellationToken) => expenseService.GetUsersAsync(cancellationToken);
}
