using Microsoft.AspNetCore.Mvc;
using ExpenseApp.Models;
using ExpenseApp.Services;

namespace ExpenseApp.Controllers;

/// <summary>
/// Expenses API - Manage expense records
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(ExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expenses
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Expense>>> GetAll()
    {
        var expenses = await _expenseService.GetAllExpensesAsync();
        return Ok(expenses);
    }

    /// <summary>
    /// Get expense by ID
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Expense>> GetById(int id)
    {
        var expense = await _expenseService.GetExpenseByIdAsync(id);
        if (expense == null) return NotFound();
        return Ok(expense);
    }

    /// <summary>
    /// Get expenses filtered by status
    /// </summary>
    [HttpGet("by-status/{statusName}")]
    public async Task<ActionResult<List<Expense>>> GetByStatus(string statusName)
    {
        var expenses = await _expenseService.GetExpensesByStatusAsync(statusName);
        return Ok(expenses);
    }

    /// <summary>
    /// Get expenses for a specific user
    /// </summary>
    [HttpGet("by-user/{userId:int}")]
    public async Task<ActionResult<List<Expense>>> GetByUser(int userId)
    {
        var expenses = await _expenseService.GetExpensesByUserAsync(userId);
        return Ok(expenses);
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateExpenseRequest request)
    {
        var newId = await _expenseService.CreateExpenseAsync(request);
        if (newId == null) return BadRequest(new { message = "Failed to create expense", error = _expenseService.LastError });
        return CreatedAtAction(nameof(GetById), new { id = newId }, new { expenseId = newId });
    }

    /// <summary>
    /// Submit a draft expense for approval
    /// </summary>
    [HttpPost("{id:int}/submit")]
    public async Task<ActionResult<object>> Submit(int id)
    {
        var success = await _expenseService.SubmitExpenseAsync(id);
        if (!success) return BadRequest(new { message = "Failed to submit expense", error = _expenseService.LastError });
        return Ok(new { message = "Expense submitted successfully" });
    }

    /// <summary>
    /// Update expense status (Approve or Reject)
    /// </summary>
    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<object>> UpdateStatus(int id, [FromBody] UpdateExpenseStatusRequest request)
    {
        request.ExpenseId = id;
        var success = await _expenseService.UpdateExpenseStatusAsync(request);
        if (!success) return BadRequest(new { message = "Failed to update expense status", error = _expenseService.LastError });
        return Ok(new { message = $"Expense status updated to {request.StatusName}" });
    }
}

/// <summary>
/// Users API - Manage users
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly ExpenseService _expenseService;

    public UsersController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ExpenseUser>>> GetAll()
    {
        var users = await _expenseService.GetAllUsersAsync();
        return Ok(users);
    }
}

/// <summary>
/// Categories API - Manage expense categories
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly ExpenseService _expenseService;

    public CategoriesController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all expense categories
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ExpenseCategory>>> GetAll()
    {
        var categories = await _expenseService.GetAllCategoriesAsync();
        return Ok(categories);
    }
}

/// <summary>
/// Statuses API - Manage expense statuses
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatusesController : ControllerBase
{
    private readonly ExpenseService _expenseService;

    public StatusesController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all expense statuses
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ExpenseStatus>>> GetAll()
    {
        var statuses = await _expenseService.GetAllStatusesAsync();
        return Ok(statuses);
    }
}

/// <summary>
/// Chat API - AI-powered expense assistant
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;

    public ChatController(ChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>
    /// Send a chat message to the AI assistant
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        var response = await _chatService.ChatAsync(request);
        return Ok(response);
    }
}
