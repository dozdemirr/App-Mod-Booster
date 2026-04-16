using Microsoft.AspNetCore.Mvc;
using ExpenseMgmt.Models;
using ExpenseMgmt.Services;

namespace ExpenseMgmt.Controllers;

/// <summary>
/// Expenses API - CRUD and workflow operations for expenses.
/// All database interactions use stored procedures via ExpenseService.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public ExpensesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all expenses with optional text filter and user filter.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Expense>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenses([FromQuery] string? filter = null, [FromQuery] int? userId = null)
    {
        var (expenses, error) = await _expenseService.GetExpensesAsync(filter, userId);
        if (error != null)
            return Ok(new { data = expenses, warning = error, isDummyData = true });
        return Ok(new { data = expenses, warning = (string?)null, isDummyData = false });
    }

    /// <summary>
    /// Get a single expense by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExpense(int id)
    {
        var (expense, error) = await _expenseService.GetExpenseByIdAsync(id);
        if (error != null) return StatusCode(503, new { error });
        if (expense == null) return NotFound(new { message = $"Expense {id} not found" });
        return Ok(expense);
    }

    /// <summary>
    /// Create a new expense (status = Draft).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var (expenseId, error) = await _expenseService.CreateExpenseAsync(request);
        if (error != null) return StatusCode(503, new { error });
        return CreatedAtAction(nameof(GetExpense), new { id = expenseId }, new { expenseId, message = "Expense created as Draft" });
    }

    /// <summary>
    /// Submit a Draft expense for approval.
    /// </summary>
    [HttpPost("{id:int}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SubmitExpense(int id, [FromBody] SubmitExpenseRequest request)
    {
        var (success, error) = await _expenseService.SubmitExpenseAsync(id, request.UserId);
        if (error != null) return StatusCode(503, new { error });
        return Ok(new { success, message = "Expense submitted for approval" });
    }

    /// <summary>
    /// Approve a submitted expense (manager action).
    /// </summary>
    [HttpPost("{id:int}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ApproveExpense(int id, [FromBody] ApproveExpenseRequest request)
    {
        var (success, error) = await _expenseService.ApproveExpenseAsync(id, request.ReviewerId);
        if (error != null) return StatusCode(503, new { error });
        return Ok(new { success, message = "Expense approved" });
    }

    /// <summary>
    /// Reject a submitted expense (manager action).
    /// </summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RejectExpense(int id, [FromBody] RejectExpenseRequest request)
    {
        var (success, error) = await _expenseService.RejectExpenseAsync(id, request.ReviewerId, request.RejectionReason);
        if (error != null) return StatusCode(503, new { error });
        return Ok(new { success, message = "Expense rejected" });
    }

    /// <summary>
    /// Get all pending (Submitted) expenses for manager review.
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IEnumerable<Expense>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingExpenses([FromQuery] string? filter = null)
    {
        var (expenses, error) = await _expenseService.GetPendingExpensesAsync(filter);
        if (error != null)
            return Ok(new { data = expenses, warning = error, isDummyData = true });
        return Ok(new { data = expenses, warning = (string?)null, isDummyData = false });
    }

    /// <summary>
    /// Delete a Draft expense.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteExpense(int id, [FromQuery] int userId)
    {
        var (success, error) = await _expenseService.DeleteExpenseAsync(id, userId);
        if (error != null) return StatusCode(503, new { error });
        return Ok(new { success, message = "Expense deleted" });
    }

    /// <summary>
    /// Get expense summary statistics grouped by status.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(IEnumerable<ExpenseSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary([FromQuery] int? userId = null)
    {
        var (summary, error) = await _expenseService.GetExpenseSummaryAsync(userId);
        if (error != null)
            return Ok(new { data = summary, warning = error, isDummyData = true });
        return Ok(new { data = summary, warning = (string?)null, isDummyData = false });
    }
}
