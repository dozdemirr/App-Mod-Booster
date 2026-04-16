using Microsoft.AspNetCore.Mvc;
using ExpenseMgmt.Services;

namespace ExpenseMgmt.Controllers;

/// <summary>
/// Chat API - natural language interface powered by Azure OpenAI with function calling.
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
    /// Send a message to the AI assistant and receive a natural language response.
    /// The AI has access to all expense data functions via function calling.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message cannot be empty" });

        var response = await _chatService.ChatAsync(request.Message, request.History ?? new List<ChatMessageDto>());
        return Ok(new ChatResponse { Reply = response });
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessageDto>? History { get; set; }
}

public class ChatResponse
{
    public string Reply { get; set; } = string.Empty;
}
