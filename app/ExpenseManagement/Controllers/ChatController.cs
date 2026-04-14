using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

public class ChatRequest
{
    public string Message { get; set; } = "";
    public List<ConversationMessage> ConversationHistory { get; set; } = new();
}

public class ConversationMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;

    public ChatController(ChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest request)
    {
        var response = await _chatService.GetResponseAsync(request.Message, request.ConversationHistory);
        return Ok(new { response });
    }
}
