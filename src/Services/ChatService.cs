using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseApp.Models;
using OpenAI.Chat;
using System.Text.Json;

namespace ExpenseApp.Services;

public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly ExpenseService _expenseService;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, ExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request)
    {
        var openAIEndpoint = _configuration["OpenAI:Endpoint"];
        var deploymentName = _configuration["OpenAI:DeploymentName"] ?? "gpt-4o";

        if (string.IsNullOrEmpty(openAIEndpoint))
        {
            return new ChatResponse
            {
                Message = "⚠️ **GenAI services not deployed.** To enable the full chat experience with real AI responses, please run `./deploy-with-chat.sh` which will deploy Azure OpenAI and configure the required settings. In the meantime, here is some demo data:\n\n" +
                          "**Sample Expenses:**\n- 1. Alice Example - Travel - £25.40 (Submitted)\n- 2. Alice Example - Meals - £14.25 (Approved)\n- 3. Alice Example - Supplies - £7.99 (Draft)",
                Success = true
            };
        }

        try
        {
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

            var client = new AzureOpenAIClient(new Uri(openAIEndpoint), credential);
            var chatClient = client.GetChatClient(deploymentName);

            // Define function tools
            var tools = new List<ChatTool>
            {
                ChatTool.CreateFunctionTool(
                    "get_all_expenses",
                    "Retrieves all expenses from the database with user, category, and status information"),
                ChatTool.CreateFunctionTool(
                    "get_expenses_by_status",
                    "Retrieves expenses filtered by status (Draft, Submitted, Approved, Rejected)",
                    BinaryData.FromString("""{"type":"object","properties":{"status_name":{"type":"string","description":"The status to filter by: Draft, Submitted, Approved, or Rejected"}},"required":["status_name"]}""")),
                ChatTool.CreateFunctionTool(
                    "get_expenses_by_user",
                    "Retrieves expenses for a specific user by their user ID",
                    BinaryData.FromString("""{"type":"object","properties":{"user_id":{"type":"integer","description":"The ID of the user"}},"required":["user_id"]}""")),
                ChatTool.CreateFunctionTool(
                    "get_expense_by_id",
                    "Retrieves a single expense by its ID",
                    BinaryData.FromString("""{"type":"object","properties":{"expense_id":{"type":"integer","description":"The ID of the expense"}},"required":["expense_id"]}""")),
                ChatTool.CreateFunctionTool(
                    "get_all_users",
                    "Retrieves all users from the system"),
                ChatTool.CreateFunctionTool(
                    "get_all_categories",
                    "Retrieves all expense categories"),
                ChatTool.CreateFunctionTool(
                    "submit_expense",
                    "Submits a draft expense for approval",
                    BinaryData.FromString("""{"type":"object","properties":{"expense_id":{"type":"integer","description":"The ID of the expense to submit"}},"required":["expense_id"]}""")),
                ChatTool.CreateFunctionTool(
                    "approve_expense",
                    "Approves a submitted expense",
                    BinaryData.FromString("""{"type":"object","properties":{"expense_id":{"type":"integer","description":"The ID of the expense to approve"},"reviewed_by":{"type":"integer","description":"The user ID of the manager approving the expense"}},"required":["expense_id","reviewed_by"]}""")),
                ChatTool.CreateFunctionTool(
                    "reject_expense",
                    "Rejects a submitted expense",
                    BinaryData.FromString("""{"type":"object","properties":{"expense_id":{"type":"integer","description":"The ID of the expense to reject"},"reviewed_by":{"type":"integer","description":"The user ID of the manager rejecting the expense"}},"required":["expense_id","reviewed_by"]}"""))
            };

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(
                    "You are an expense management assistant. You have access to real functions to interact with the expense database. " +
                    "Use the available functions to retrieve and manage expense data when users ask questions. " +
                    "When displaying lists of items, format them clearly with bullet points or numbered lists. " +
                    "Always show amounts in GBP format (e.g. £25.40). " +
                    "Available capabilities: view all expenses, filter by status or user, view expense details, submit drafts for approval, approve/reject expenses, view users and categories.")
            };

            foreach (var historyMsg in request.History)
            {
                if (historyMsg.Role == "user")
                    messages.Add(ChatMessage.CreateUserMessage(historyMsg.Content));
                else if (historyMsg.Role == "assistant")
                    messages.Add(ChatMessage.CreateAssistantMessage(historyMsg.Content));
            }

            messages.Add(ChatMessage.CreateUserMessage(request.Message));

            var options = new ChatCompletionOptions();
            foreach (var tool in tools)
                options.Tools.Add(tool);

            // Agentic loop
            var maxIterations = 5;
            for (int i = 0; i < maxIterations; i++)
            {
                var completion = await chatClient.CompleteChatAsync(messages, options);
                var response = completion.Value;

                if (response.FinishReason == ChatFinishReason.ToolCalls)
                {
                    var assistantMessage = ChatMessage.CreateAssistantMessage(response);
                    messages.Add(assistantMessage);

                    foreach (var toolCall in response.ToolCalls)
                    {
                        var result = await ExecuteToolAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                        messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, result));
                    }
                    continue;
                }

                var finalContent = response.Content.FirstOrDefault()?.Text ?? "I couldn't generate a response.";
                return new ChatResponse { Message = finalContent, Success = true };
            }

            return new ChatResponse { Message = "I reached the maximum number of tool calls. Please try a simpler request.", Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat service error");
            return new ChatResponse
            {
                Message = "I encountered an error processing your request. Please check that Azure OpenAI is configured correctly.",
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<string> ExecuteToolAsync(string functionName, string arguments)
    {
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            switch (functionName)
            {
                case "get_all_expenses":
                {
                    var expenses = await _expenseService.GetAllExpensesAsync();
                    return JsonSerializer.Serialize(expenses);
                }
                case "get_expenses_by_status":
                {
                    var statusName = root.GetProperty("status_name").GetString() ?? "";
                    var expenses = await _expenseService.GetExpensesByStatusAsync(statusName);
                    return JsonSerializer.Serialize(expenses);
                }
                case "get_expenses_by_user":
                {
                    var userId = root.GetProperty("user_id").GetInt32();
                    var expenses = await _expenseService.GetExpensesByUserAsync(userId);
                    return JsonSerializer.Serialize(expenses);
                }
                case "get_expense_by_id":
                {
                    var expenseId = root.GetProperty("expense_id").GetInt32();
                    var expense = await _expenseService.GetExpenseByIdAsync(expenseId);
                    return expense == null ? "{\"error\":\"Expense not found\"}" : JsonSerializer.Serialize(expense);
                }
                case "get_all_users":
                {
                    var users = await _expenseService.GetAllUsersAsync();
                    return JsonSerializer.Serialize(users);
                }
                case "get_all_categories":
                {
                    var categories = await _expenseService.GetAllCategoriesAsync();
                    return JsonSerializer.Serialize(categories);
                }
                case "submit_expense":
                {
                    var expenseId = root.GetProperty("expense_id").GetInt32();
                    var success = await _expenseService.SubmitExpenseAsync(expenseId);
                    return JsonSerializer.Serialize(new { success, message = success ? "Expense submitted successfully" : "Failed to submit expense" });
                }
                case "approve_expense":
                {
                    var expenseId = root.GetProperty("expense_id").GetInt32();
                    var reviewedBy = root.GetProperty("reviewed_by").GetInt32();
                    var success = await _expenseService.UpdateExpenseStatusAsync(new UpdateExpenseStatusRequest
                    {
                        ExpenseId = expenseId,
                        StatusName = "Approved",
                        ReviewedBy = reviewedBy
                    });
                    return JsonSerializer.Serialize(new { success, message = success ? "Expense approved successfully" : "Failed to approve expense" });
                }
                case "reject_expense":
                {
                    var expenseId = root.GetProperty("expense_id").GetInt32();
                    var reviewedBy = root.GetProperty("reviewed_by").GetInt32();
                    var success = await _expenseService.UpdateExpenseStatusAsync(new UpdateExpenseStatusRequest
                    {
                        ExpenseId = expenseId,
                        StatusName = "Rejected",
                        ReviewedBy = reviewedBy
                    });
                    return JsonSerializer.Serialize(new { success, message = success ? "Expense rejected successfully" : "Failed to reject expense" });
                }
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
