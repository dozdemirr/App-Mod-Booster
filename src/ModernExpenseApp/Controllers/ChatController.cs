using Microsoft.AspNetCore.Mvc;
using ModernExpenseApp.Models;
using ModernExpenseApp.Services;

namespace ModernExpenseApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ChatController(IChatService chatService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var response = await chatService.AskAsync(request.Message, cancellationToken);
        return Ok(new { response });
    }
}
