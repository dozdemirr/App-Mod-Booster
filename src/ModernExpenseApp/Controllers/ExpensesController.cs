using Microsoft.AspNetCore.Mvc;
using ModernExpenseApp.Models;
using ModernExpenseApp.Services;

namespace ModernExpenseApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ExpensesController(IExpenseService expenseService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseDto>>> GetExpenses([FromQuery] int? statusId, [FromQuery] int? userId, CancellationToken cancellationToken)
        => Ok(await expenseService.GetExpensesAsync(statusId, userId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<object>> CreateExpense([FromBody] CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        var createdId = await expenseService.CreateExpenseAsync(request, cancellationToken);
        return Ok(new { createdId });
    }

    [HttpPost("{expenseId:int}/review")]
    public async Task<IActionResult> ReviewExpense(int expenseId, [FromBody] ReviewExpenseRequest request, CancellationToken cancellationToken)
    {
        await expenseService.ReviewExpenseAsync(expenseId, request.ManagerUserId, request.Approve, cancellationToken);
        return NoContent();
    }
}
