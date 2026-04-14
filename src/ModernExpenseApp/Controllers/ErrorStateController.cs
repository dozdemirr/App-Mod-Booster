using Microsoft.AspNetCore.Mvc;
using ModernExpenseApp.Services;

namespace ModernExpenseApp.Controllers;

[ApiController]
[Route("api/errors")]
public sealed class ErrorStateController(ErrorBannerService errorBannerService) : ControllerBase
{
    [HttpGet("current")]
    public IActionResult GetCurrent() => Ok(new { message = errorBannerService.CurrentError });
}
