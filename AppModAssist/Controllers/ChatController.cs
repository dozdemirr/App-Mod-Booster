using AppModAssist.Models;
using AppModAssist.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppModAssist.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;

    public ChatController(ChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Ask([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _chatService.AskAsync(request.Message, cancellationToken));
    }
}
