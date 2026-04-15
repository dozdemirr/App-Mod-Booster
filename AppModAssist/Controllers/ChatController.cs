using AppModAssist.Models;
using AppModAssist.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppModAssist.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ChatController(IChatService chatService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Ask([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var response = await chatService.AskAsync(request.Message, cancellationToken);
        return Ok(response);
    }
}
