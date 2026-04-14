using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModel
{
    private readonly IConfiguration _configuration;

    public bool IsGenAIConfigured { get; private set; }

    public ChatModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnGet()
    {
        var endpoint = _configuration["OpenAI:Endpoint"];
        IsGenAIConfigured = !string.IsNullOrEmpty(endpoint) && endpoint != "<OPENAI_ENDPOINT>";
        ViewData["ActivePage"] = "Chat";
    }
}
