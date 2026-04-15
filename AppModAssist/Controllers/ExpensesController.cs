using AppModAssist.Models;
using AppModAssist.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppModAssist.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ExpensesController(IExpenseService expenseService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseItem>>> GetExpenses([FromQuery] string? status, [FromQuery] int? userId, [FromQuery] int? categoryId, CancellationToken cancellationToken)
    {
        var result = await expenseService.GetExpensesAsync(new ExpenseFilter(status, userId, categoryId), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<object>> CreateExpense([FromBody] CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        var id = await expenseService.CreateExpenseAsync(request, cancellationToken);
        return Ok(new { expenseId = id });
    }

    [HttpPost("status")]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateExpenseStatusRequest request, CancellationToken cancellationToken)
    {
        await expenseService.UpdateExpenseStatusAsync(request, cancellationToken);
        return NoContent();
    }
}
