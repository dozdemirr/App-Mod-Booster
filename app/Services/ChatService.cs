using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseMgmt.Models;
using OpenAI.Chat;
using System.Text.Json;
using System.ClientModel;

namespace ExpenseMgmt.Services;

/// <summary>
/// ChatService: Integrates Azure OpenAI with function calling to allow natural language
/// interaction with the Expense Management database via the existing service layer.
/// Uses ManagedIdentityCredential with the app's user-assigned managed identity.
/// </summary>
public class ChatService
{
    private readonly IExpenseService _expenseService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IExpenseService expenseService, IConfiguration configuration, ILogger<ChatService> logger)
    {
        _expenseService = expenseService;
        _configuration = configuration;
        _logger = logger;
    }

    private AzureOpenAIClient? CreateOpenAIClient()
    {
        var endpoint = _configuration["OpenAI:Endpoint"];
        if (string.IsNullOrEmpty(endpoint))
        {
            _logger.LogWarning("OpenAI:Endpoint is not configured. GenAI features unavailable.");
            return null;
        }

        var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
        Azure.Core.TokenCredential credential;

        if (!string.IsNullOrEmpty(managedIdentityClientId))
        {
            _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
            credential = new ManagedIdentityCredential(managedIdentityClientId);
        }
        else
        {
            _logger.LogInformation("Using DefaultAzureCredential");
            credential = new DefaultAzureCredential();
        }

        return new AzureOpenAIClient(new Uri(endpoint), credential);
    }

    public async Task<string> ChatAsync(string userMessage, List<ChatMessageDto> history)
    {
        var client = CreateOpenAIClient();
        if (client == null)
        {
            return "⚠️ **GenAI services are not deployed.** The chat UI is available but AI responses require Azure OpenAI to be deployed. " +
                   "Run **deploy-with-chat.sh** to deploy the GenAI resources and unlock AI-powered natural language interactions with your expense data.";
        }

        var deploymentName = _configuration["OpenAI:DeploymentName"] ?? "gpt-4o";

        try
        {
            var chatClient = client.GetChatClient(deploymentName);

            // Define function tools for all expense operations
            var tools = new List<ChatTool>
            {
                ChatTool.CreateFunctionTool(
                    "get_expenses",
                    "Retrieves a list of expenses from the system. Can filter by text (category, status, description, user name). Optionally filter by user ID.",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "filter": { "type": "string", "description": "Optional text filter for category, status, description or user name" },
                            "userId": { "type": "integer", "description": "Optional user ID to filter expenses for a specific user" }
                        }
                    }
                    """)),
                ChatTool.CreateFunctionTool(
                    "get_pending_expenses",
                    "Retrieves all pending (Submitted) expenses awaiting manager approval.",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "filter": { "type": "string", "description": "Optional text filter" }
                        }
                    }
                    """)),
                ChatTool.CreateFunctionTool(
                    "create_expense",
                    "Creates a new expense claim as a Draft. Amount should be in GBP (e.g., 12.50 for £12.50).",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "userId": { "type": "integer", "description": "ID of the user submitting the expense" },
                            "categoryId": { "type": "integer", "description": "Category ID (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)" },
                            "amountGBP": { "type": "number", "description": "Amount in GBP (e.g., 12.50)" },
                            "expenseDate": { "type": "string", "description": "Date of expense in YYYY-MM-DD format" },
                            "description": { "type": "string", "description": "Description of the expense" }
                        },
                        "required": ["userId", "categoryId", "amountGBP", "expenseDate"]
                    }
                    """)),
                ChatTool.CreateFunctionTool(
                    "approve_expense",
                    "Approves a submitted expense claim. Only for managers.",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "expenseId": { "type": "integer", "description": "ID of the expense to approve" },
                            "reviewerId": { "type": "integer", "description": "User ID of the manager approving" }
                        },
                        "required": ["expenseId", "reviewerId"]
                    }
                    """)),
                ChatTool.CreateFunctionTool(
                    "reject_expense",
                    "Rejects a submitted expense claim. Only for managers.",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "expenseId": { "type": "integer", "description": "ID of the expense to reject" },
                            "reviewerId": { "type": "integer", "description": "User ID of the manager rejecting" },
                            "rejectionReason": { "type": "string", "description": "Reason for rejection" }
                        },
                        "required": ["expenseId", "reviewerId"]
                    }
                    """)),
                ChatTool.CreateFunctionTool(
                    "get_categories",
                    "Retrieves all available expense categories.",
                    BinaryData.FromString("""{ "type": "object", "properties": {} }""")),
                ChatTool.CreateFunctionTool(
                    "get_users",
                    "Retrieves all active users in the system.",
                    BinaryData.FromString("""{ "type": "object", "properties": {} }""")),
                ChatTool.CreateFunctionTool(
                    "get_expense_summary",
                    "Gets a summary of expenses grouped by status with totals.",
                    BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "userId": { "type": "integer", "description": "Optional user ID to filter summary for a specific user" }
                        }
                    }
                    """))
            };

            var systemPrompt = """
                You are an intelligent assistant for the Expense Management System.
                You have access to real functions to interact with expense data in the database.
                
                Available capabilities:
                - List and filter expenses by category, status, date, or user
                - Show pending expenses awaiting approval
                - Create new expense claims (as Draft status)
                - Approve or reject submitted expenses (manager function)
                - Get expense categories and user information
                - Provide expense summaries and totals
                
                When presenting lists of expenses, format them clearly with:
                - Expense ID, date, category, amount (in £), status, and description
                - Group or sort information logically
                - Use bold for important values like amounts
                
                For amounts, always display in £ format (e.g., £12.50).
                Dates should be formatted as DD/MM/YYYY.
                Be helpful, concise, and professional.
                """;

            // Build message history
            var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };
            foreach (var h in history.TakeLast(10))
            {
                if (h.Role == "user") messages.Add(new UserChatMessage(h.Content));
                else if (h.Role == "assistant") messages.Add(new AssistantChatMessage(h.Content));
            }
            messages.Add(new UserChatMessage(userMessage));

            var options = new ChatCompletionOptions();
            foreach (var tool in tools) options.Tools.Add(tool);

            // Function calling loop
            string finalResponse = string.Empty;
            int maxIterations = 5;
            int iteration = 0;

            while (iteration++ < maxIterations)
            {
                var response = await chatClient.CompleteChatAsync(messages, options);
                var completion = response.Value;

                if (completion.FinishReason == ChatFinishReason.ToolCalls)
                {
                    // Execute tool calls
                    messages.Add(new AssistantChatMessage(completion));
                    var toolResultMessages = new List<ToolChatMessage>();

                    foreach (var toolCall in completion.ToolCalls)
                    {
                        var result = await ExecuteToolCallAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                        toolResultMessages.Add(new ToolChatMessage(toolCall.Id, result));
                    }

                    messages.AddRange(toolResultMessages);
                }
                else
                {
                    finalResponse = completion.Content[0].Text;
                    break;
                }
            }

            return string.IsNullOrEmpty(finalResponse)
                ? "I processed your request but couldn't generate a response. Please try again."
                : finalResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChatAsync");
            return $"⚠️ Chat error: {ex.Message}. Please check that Azure OpenAI is configured correctly and the managed identity has the 'Cognitive Services OpenAI User' role.";
        }
    }

    private async Task<string> ExecuteToolCallAsync(string functionName, string argumentsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var args = doc.RootElement;

            switch (functionName)
            {
                case "get_expenses":
                {
                    var filter = args.TryGetProperty("filter", out var f) ? f.GetString() : null;
                    int? userId = args.TryGetProperty("userId", out var u) ? u.GetInt32() : null;
                    var (expenses, error) = await _expenseService.GetExpensesAsync(filter, userId);
                    return error != null
                        ? JsonSerializer.Serialize(new { error, expenses })
                        : JsonSerializer.Serialize(expenses);
                }
                case "get_pending_expenses":
                {
                    var filter = args.TryGetProperty("filter", out var f) ? f.GetString() : null;
                    var (expenses, error) = await _expenseService.GetPendingExpensesAsync(filter);
                    return error != null
                        ? JsonSerializer.Serialize(new { error, expenses })
                        : JsonSerializer.Serialize(expenses);
                }
                case "create_expense":
                {
                    var request = new CreateExpenseRequest
                    {
                        UserId = args.GetProperty("userId").GetInt32(),
                        CategoryId = args.GetProperty("categoryId").GetInt32(),
                        AmountGBP = args.GetProperty("amountGBP").GetDecimal(),
                        ExpenseDate = DateTime.Parse(args.GetProperty("expenseDate").GetString()!),
                        Description = args.TryGetProperty("description", out var d) ? d.GetString() : null
                    };
                    var (expenseId, error) = await _expenseService.CreateExpenseAsync(request);
                    return error != null
                        ? JsonSerializer.Serialize(new { error })
                        : JsonSerializer.Serialize(new { success = true, expenseId, message = $"Expense created with ID {expenseId} as Draft" });
                }
                case "approve_expense":
                {
                    var expenseId = args.GetProperty("expenseId").GetInt32();
                    var reviewerId = args.GetProperty("reviewerId").GetInt32();
                    var (success, error) = await _expenseService.ApproveExpenseAsync(expenseId, reviewerId);
                    return JsonSerializer.Serialize(new { success, error });
                }
                case "reject_expense":
                {
                    var expenseId = args.GetProperty("expenseId").GetInt32();
                    var reviewerId = args.GetProperty("reviewerId").GetInt32();
                    var reason = args.TryGetProperty("rejectionReason", out var r) ? r.GetString() : null;
                    var (success, error) = await _expenseService.RejectExpenseAsync(expenseId, reviewerId, reason);
                    return JsonSerializer.Serialize(new { success, error });
                }
                case "get_categories":
                {
                    var (categories, error) = await _expenseService.GetCategoriesAsync();
                    return error != null
                        ? JsonSerializer.Serialize(new { error, categories })
                        : JsonSerializer.Serialize(categories);
                }
                case "get_users":
                {
                    var (users, error) = await _expenseService.GetUsersAsync();
                    return error != null
                        ? JsonSerializer.Serialize(new { error, users })
                        : JsonSerializer.Serialize(users);
                }
                case "get_expense_summary":
                {
                    int? userId = args.TryGetProperty("userId", out var u) ? u.GetInt32() : null;
                    var (summary, error) = await _expenseService.GetExpenseSummaryAsync(userId);
                    return error != null
                        ? JsonSerializer.Serialize(new { error, summary })
                        : JsonSerializer.Serialize(summary);
                }
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool call {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}

public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
