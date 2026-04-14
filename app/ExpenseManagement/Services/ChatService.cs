using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseManagement.Models;
using System.Text.Json;

namespace ExpenseManagement.Services;

public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly ExpenseService _expenseService;
    private readonly UserService _userService;

    private const string SystemPrompt =
        "You are an AI assistant for an Expense Management System. You help employees and managers manage expenses. " +
        "You have access to functions to read and manage expenses. When users ask to list or show data, use the " +
        "appropriate function and then present the results in a nicely formatted list. Always present amounts in GBP " +
        "format (£XX.XX). Be helpful, professional and concise.";

    public ChatService(
        IConfiguration configuration,
        ILogger<ChatService> logger,
        ExpenseService expenseService,
        UserService userService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
        _userService = userService;
    }

    private bool IsOpenAIConfigured()
    {
        var endpoint = _configuration["OpenAI:Endpoint"];
        return !string.IsNullOrEmpty(endpoint) && endpoint != "<OPENAI_ENDPOINT>";
    }

    public async Task<string> GetResponseAsync(string userMessage, List<ExpenseManagement.Controllers.ConversationMessage> conversationHistory)
    {
        if (!IsOpenAIConfigured())
        {
            var (dummyExpenses, _) = _expenseService.GetAllExpenses();
            var expenseList = string.Join("\n", dummyExpenses.Select(e =>
                $"  - #{e.ExpenseId} {e.Description} by {e.UserName}: £{e.AmountGBP:F2} ({e.StatusName})"));
            return $"GenAI services are not deployed. Please run deploy-with-chat.sh to enable the full AI chat experience. " +
                   $"For now, here's some sample data:\n\n**Recent Expenses:**\n{expenseList}";
        }

        try
        {
            var endpoint = _configuration["OpenAI:Endpoint"]!;
            var deploymentName = _configuration["OpenAI:DeploymentName"] ?? "gpt-4o";
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];

            Azure.Core.TokenCredential credential;
            if (!string.IsNullOrEmpty(managedIdentityClientId) && managedIdentityClientId != "<MANAGED_IDENTITY_CLIENT_ID>")
            {
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                credential = new DefaultAzureCredential();
            }

            var client = new OpenAIClient(new Uri(endpoint), credential);
            var tools = BuildToolDefinitions();

            var chatOptions = new ChatCompletionsOptions
            {
                DeploymentName = deploymentName,
                ToolChoice = ChatCompletionsToolChoice.Auto
            };

            foreach (var tool in tools)
                chatOptions.Tools.Add(tool);

            chatOptions.Messages.Add(new ChatRequestSystemMessage(SystemPrompt));

            foreach (var msg in conversationHistory)
            {
                ChatRequestMessage chatMsg = msg.Role.ToLower() switch
                {
                    "user" => new ChatRequestUserMessage(msg.Content),
                    "assistant" => new ChatRequestAssistantMessage(msg.Content),
                    _ => new ChatRequestUserMessage(msg.Content)
                };
                chatOptions.Messages.Add(chatMsg);
            }

            chatOptions.Messages.Add(new ChatRequestUserMessage(userMessage));

            // Function calling loop
            for (int i = 0; i < 10; i++)
            {
                var response = await client.GetChatCompletionsAsync(chatOptions);
                var choice = response.Value.Choices[0];

                if (choice.FinishReason == CompletionsFinishReason.ToolCalls)
                {
                    var assistantMessage = new ChatRequestAssistantMessage(choice.Message.Content ?? "");
                    foreach (var toolCall in choice.Message.ToolCalls)
                        assistantMessage.ToolCalls.Add(toolCall);
                    chatOptions.Messages.Add(assistantMessage);

                    foreach (var toolCall in choice.Message.ToolCalls)
                    {
                        if (toolCall is ChatCompletionsFunctionToolCall funcCall)
                        {
                            var result = ExecuteFunction(funcCall.Name, funcCall.Arguments);
                            chatOptions.Messages.Add(new ChatRequestToolMessage(result, funcCall.Id));
                        }
                    }
                    continue;
                }

                return choice.Message.Content ?? "I'm sorry, I couldn't generate a response.";
            }

            return "I'm sorry, the conversation loop exceeded the maximum iterations.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Azure OpenAI");
            return $"I encountered an error: {ex.Message}. Please try again.";
        }
    }

    private string ExecuteFunction(string functionName, string arguments)
    {
        try
        {
            var args = JsonDocument.Parse(arguments).RootElement;

            return functionName switch
            {
                "get_all_expenses" => ExecuteGetAllExpenses(),
                "get_expenses_by_status" => ExecuteGetExpensesByStatus(args),
                "get_expenses_by_user" => ExecuteGetExpensesByUser(args),
                "get_expense_summary" => ExecuteGetExpenseSummary(),
                "get_all_users" => ExecuteGetAllUsers(),
                "create_expense" => ExecuteCreateExpense(args),
                "submit_expense" => ExecuteSubmitExpense(args),
                "approve_expense" => ExecuteApproveExpense(args),
                "reject_expense" => ExecuteRejectExpense(args),
                _ => $"Unknown function: {functionName}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return $"Error executing {functionName}: {ex.Message}";
        }
    }

    private string ExecuteGetAllExpenses()
    {
        var (expenses, error) = _expenseService.GetAllExpenses();
        if (error != null) return $"Error: {error}";
        return JsonSerializer.Serialize(expenses.Select(e => new
        {
            e.ExpenseId, e.UserName, e.CategoryName, e.StatusName,
            AmountGBP = e.AmountGBP, e.Currency, ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
            e.Description
        }));
    }

    private string ExecuteGetExpensesByStatus(JsonElement args)
    {
        var status = args.GetProperty("status").GetString() ?? "";
        var (expenses, error) = _expenseService.GetExpensesByStatus(status);
        if (error != null) return $"Error: {error}";
        return JsonSerializer.Serialize(expenses.Select(e => new
        {
            e.ExpenseId, e.UserName, e.CategoryName, e.StatusName,
            AmountGBP = e.AmountGBP, e.Currency, ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
            e.Description
        }));
    }

    private string ExecuteGetExpensesByUser(JsonElement args)
    {
        var userId = args.GetProperty("userId").GetInt32();
        var (expenses, error) = _expenseService.GetExpensesByUser(userId);
        if (error != null) return $"Error: {error}";
        return JsonSerializer.Serialize(expenses.Select(e => new
        {
            e.ExpenseId, e.UserName, e.CategoryName, e.StatusName,
            AmountGBP = e.AmountGBP, e.Currency, ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
            e.Description
        }));
    }

    private string ExecuteGetExpenseSummary()
    {
        var (summary, error) = _expenseService.GetExpenseSummary();
        if (error != null) return $"Error: {error}";
        return JsonSerializer.Serialize(summary);
    }

    private string ExecuteGetAllUsers()
    {
        var (users, error) = _userService.GetAllUsers();
        if (error != null) return $"Error: {error}";
        return JsonSerializer.Serialize(users.Select(u => new
        {
            u.UserId, u.UserName, u.Email, u.RoleName, u.ManagerName, u.IsActive
        }));
    }

    private string ExecuteCreateExpense(JsonElement args)
    {
        var request = new CreateExpenseRequest
        {
            UserId = args.GetProperty("userId").GetInt32(),
            CategoryId = args.GetProperty("categoryId").GetInt32(),
            AmountGBP = args.GetProperty("amountGbp").GetDecimal(),
            Currency = args.TryGetProperty("currency", out var currency) ? currency.GetString() ?? "GBP" : "GBP",
            ExpenseDate = DateTime.Parse(args.GetProperty("expenseDate").GetString()!),
            Description = args.GetProperty("description").GetString() ?? ""
        };
        var (success, error) = _expenseService.CreateExpense(request);
        return success ? "Expense created successfully." : $"Failed to create expense: {error}";
    }

    private string ExecuteSubmitExpense(JsonElement args)
    {
        var expenseId = args.GetProperty("expenseId").GetInt32();
        var (success, error) = _expenseService.SubmitExpense(expenseId);
        return success ? $"Expense #{expenseId} submitted for approval." : $"Failed to submit expense: {error}";
    }

    private string ExecuteApproveExpense(JsonElement args)
    {
        var expenseId = args.GetProperty("expenseId").GetInt32();
        var reviewedBy = args.GetProperty("reviewedBy").GetInt32();
        var (success, error) = _expenseService.ApproveExpense(expenseId, reviewedBy);
        return success ? $"Expense #{expenseId} approved." : $"Failed to approve expense: {error}";
    }

    private string ExecuteRejectExpense(JsonElement args)
    {
        var expenseId = args.GetProperty("expenseId").GetInt32();
        var reviewedBy = args.GetProperty("reviewedBy").GetInt32();
        var (success, error) = _expenseService.RejectExpense(expenseId, reviewedBy);
        return success ? $"Expense #{expenseId} rejected." : $"Failed to reject expense: {error}";
    }

    private static List<ChatCompletionsToolDefinition> BuildToolDefinitions() => new()
    {
        new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
        {
            Name = "get_all_expenses",
            Description = "Retrieves all expenses from the system",
            Parameters = BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
        }),
        new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
        {
            Name = "get_expenses_by_status",
            Description = "Gets expenses filtered by status (Draft/Submitted/Approved/Rejected)",
            Parameters = BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"status\":{\"type\":\"string\",\"description\":\"The status to filter by: Draft, Submitted, Approved, or Rejected\"}},\"required\":[\"status\"]}")
        }),
        new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
        {
            Name = "get_expenses_by_user",
            Description = "Gets expenses for a specific user",
            Parameters = BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"userId\":{\"type\":\"integer\",\"description\":\"The user ID\"}},\"required\":[\"userId\"]}")
        }),
        new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
        {
            Name = "get_expense_summary",
            Description = "Gets expense statistics and totals by status",
            Parameters = BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
        }),
        new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
        {
            Name = "get_all_users",
            Description = "Retrieves all users in the system",
            Parameters = BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
        }),
        new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
        {
            Name = "create_expense",
            Description = "Creates a new expense",
            Parameters = BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"userId\":{\"type\":\"integer\"},\"categoryId\":{\"type\":\"integer\"},\"amountGbp\":{\"type\":\"number\"},\"currency\":{\"type\":\"string\"},\"expenseDate\":{\"type\":\"string\",\"description\":\"Date in YYYY-MM-DD format\"},\"description\":{\"type\":\"string\"}},\"required\":[\"userId\",\"categoryId\",\"amountGbp\",\"expenseDate\",\"description\"]}")
        }),
        new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
        {
            Name = "submit_expense",
            Description = "Submits an expense for approval",
            Parameters = BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\"}},\"required\":[\"expenseId\"]}")
        }),
        new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
        {
            Name = "approve_expense",
            Description = "Approves an expense",
            Parameters = BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\"},\"reviewedBy\":{\"type\":\"integer\",\"description\":\"User ID of the approver\"}},\"required\":[\"expenseId\",\"reviewedBy\"]}")
        }),
        new ChatCompletionsFunctionToolDefinition(new FunctionDefinition
        {
            Name = "reject_expense",
            Description = "Rejects an expense",
            Parameters = BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\"},\"reviewedBy\":{\"type\":\"integer\",\"description\":\"User ID of the reviewer\"}},\"required\":[\"expenseId\",\"reviewedBy\"]}")
        })
    };
}
