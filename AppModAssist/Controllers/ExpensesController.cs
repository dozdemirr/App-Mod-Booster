using AppModAssist.Models;
using AppModAssist.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppModAssist.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseDataService _expenseDataService;

    public ExpensesController(IExpenseDataService expenseDataService)
    {
        _expenseDataService = expenseDataService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardData>>> Get([FromQuery] int? userId, [FromQuery] int? categoryId, [FromQuery] int? statusId, CancellationToken cancellationToken)
    {
        return Ok(await _expenseDataService.GetDashboardAsync(userId, categoryId, statusId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ExpenseItem>>> Create([FromBody] CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _expenseDataService.CreateExpenseAsync(request, cancellationToken));
    }

    [HttpPost("submit")]
    public async Task<ActionResult<ApiResponse<bool>>> Submit([FromBody] SubmitExpenseRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _expenseDataService.SubmitExpenseAsync(request, cancellationToken));
    }

    [HttpPost("review")]
    public async Task<ActionResult<ApiResponse<bool>>> Review([FromBody] ReviewExpenseRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _expenseDataService.ReviewExpenseAsync(request, cancellationToken));
    }
}
