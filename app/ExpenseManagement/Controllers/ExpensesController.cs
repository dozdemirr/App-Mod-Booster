using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly ExpenseService _expenseService;

    public ExpensesController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var (expenses, error) = _expenseService.GetAllExpenses();
        return Ok(new { data = expenses, error });
    }

    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        var (summary, error) = _expenseService.GetExpenseSummary();
        return Ok(new { data = summary, error });
    }

    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        var (expense, error) = _expenseService.GetExpenseById(id);
        if (expense == null) return NotFound();
        return Ok(new { data = expense, error });
    }

    [HttpGet("user/{userId}")]
    public IActionResult GetByUser(int userId)
    {
        var (expenses, error) = _expenseService.GetExpensesByUser(userId);
        return Ok(new { data = expenses, error });
    }

    [HttpGet("status/{statusName}")]
    public IActionResult GetByStatus(string statusName)
    {
        var (expenses, error) = _expenseService.GetExpensesByStatus(statusName);
        return Ok(new { data = expenses, error });
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateExpenseRequest request)
    {
        var (success, error) = _expenseService.CreateExpense(request);
        if (!success) return BadRequest(new { error });
        return Ok(new { success = true });
    }

    [HttpPut("{id}")]
    public IActionResult Update(int id, [FromBody] UpdateExpenseRequest request)
    {
        var (success, error) = _expenseService.UpdateExpense(id, request);
        if (!success) return BadRequest(new { error });
        return Ok(new { success = true });
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        var (success, error) = _expenseService.DeleteExpense(id);
        if (!success) return BadRequest(new { error });
        return Ok(new { success = true });
    }

    [HttpPost("{id}/submit")]
    public IActionResult Submit(int id)
    {
        var (success, error) = _expenseService.SubmitExpense(id);
        if (!success) return BadRequest(new { error });
        return Ok(new { success = true });
    }

    [HttpPost("{id}/approve")]
    public IActionResult Approve(int id, [FromBody] ReviewRequest request)
    {
        var (success, error) = _expenseService.ApproveExpense(id, request.ReviewedBy);
        if (!success) return BadRequest(new { error });
        return Ok(new { success = true });
    }

    [HttpPost("{id}/reject")]
    public IActionResult Reject(int id, [FromBody] ReviewRequest request)
    {
        var (success, error) = _expenseService.RejectExpense(id, request.ReviewedBy);
        if (!success) return BadRequest(new { error });
        return Ok(new { success = true });
    }
}

public class ReviewRequest
{
    public int ReviewedBy { get; set; }
}
