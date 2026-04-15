using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AppModAssist.Models;
using Azure.Core;
using Azure.Identity;

namespace AppModAssist.Services;

public sealed class ChatService(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    IExpenseService expenseService,
    ILogger<ChatService> logger) : IChatService
{
    public async Task<ChatResponse> AskAsync(string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new ChatResponse("Please enter a message.", false);
        }

        var endpoint = configuration["OpenAI:Endpoint"];
        var deployment = configuration["OpenAI:DeploymentName"];
        var apiVersion = configuration["OpenAI:ApiVersion"] ?? "2024-10-21";

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            return new ChatResponse(
                "GenAI services are not deployed yet. Run bash deploy-with-chat.sh to enable Azure OpenAI chat, then reopen /Index.",
                false);
        }

        try
        {
            var reply = await ExecuteFunctionCallingLoopAsync(message, endpoint, deployment, apiVersion, cancellationToken);
            return new ChatResponse(reply, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat function calling failed.");
            return new ChatResponse($"I couldn't complete that with GenAI right now. {ex.Message}", false);
        }
    }

    private async Task<string> ExecuteFunctionCallingLoopAsync(string userMessage, string endpoint, string deployment, string apiVersion, CancellationToken cancellationToken)
    {
        var toolDefinitions = BuildToolDefinitions();
        var messages = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["role"] = "system",
                ["content"] = "You are an expense assistant. Use available tools to read or update expense data. Use tools when data is needed. Summarize results clearly for end users."
            },
            new()
            {
                ["role"] = "user",
                ["content"] = userMessage
            }
        };

        for (var i = 0; i < 3; i++)
        {
            var response = await SendChatRequestAsync(messages, toolDefinitions, endpoint, deployment, apiVersion, cancellationToken);
            var choice = response.RootElement.GetProperty("choices")[0];
            var message = choice.GetProperty("message");

            if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array && toolCalls.GetArrayLength() > 0)
            {
                var assistantMessage = new Dictionary<string, object?>
                {
                    ["role"] = "assistant",
                    ["tool_calls"] = JsonSerializer.Deserialize<object>(toolCalls.GetRawText())
                };
                messages.Add(assistantMessage);

                foreach (var call in toolCalls.EnumerateArray())
                {
                    var id = call.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
                    var function = call.GetProperty("function");
                    var functionName = function.GetProperty("name").GetString() ?? string.Empty;
                    var argsJson = function.GetProperty("arguments").GetString() ?? "{}";
                    var toolResult = await ExecuteToolAsync(functionName, argsJson, cancellationToken);

                    messages.Add(new Dictionary<string, object?>
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = id,
                        ["name"] = functionName,
                        ["content"] = toolResult
                    });
                }

                continue;
            }

            var final = message.GetProperty("content").GetString();
            return string.IsNullOrWhiteSpace(final) ? "No response generated." : final;
        }

        return "The assistant reached the maximum tool-call depth.";
    }

    private async Task<string> ExecuteToolAsync(string functionName, string argsJson, CancellationToken cancellationToken)
    {
        try
        {
            using var args = JsonDocument.Parse(argsJson);
            var root = args.RootElement;

            return functionName switch
            {
                "get_expenses" => JsonSerializer.Serialize(await expenseService.GetExpensesAsync(
                    new ExpenseFilter(
                        root.TryGetProperty("status", out var status) ? status.GetString() : null,
                        root.TryGetProperty("userId", out var userId) && userId.TryGetInt32(out var parsedUserId) ? parsedUserId : null,
                        root.TryGetProperty("categoryId", out var categoryId) && categoryId.TryGetInt32(out var parsedCategoryId) ? parsedCategoryId : null),
                    cancellationToken)),
                "create_expense" => JsonSerializer.Serialize(new
                {
                    expenseId = await expenseService.CreateExpenseAsync(new CreateExpenseRequest(
                        root.GetProperty("userId").GetInt32(),
                        root.GetProperty("categoryId").GetInt32(),
                        root.GetProperty("amountMinor").GetInt32(),
                        DateOnly.Parse(root.GetProperty("expenseDate").GetString() ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")),
                        root.TryGetProperty("description", out var description) ? description.GetString() : null), cancellationToken)
                }),
                "update_expense_status" => JsonSerializer.Serialize(new
                {
                    updated = await UpdateStatusAsync(root, cancellationToken)
                }),
                _ => JsonSerializer.Serialize(new { error = $"Unknown function '{functionName}'." })
            };
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<bool> UpdateStatusAsync(JsonElement root, CancellationToken cancellationToken)
    {
        await expenseService.UpdateExpenseStatusAsync(new UpdateExpenseStatusRequest(
            root.GetProperty("expenseId").GetInt32(),
            root.GetProperty("newStatus").GetString() ?? "Submitted",
            root.GetProperty("reviewedByUserId").GetInt32()), cancellationToken);

        return true;
    }

    private static object[] BuildToolDefinitions() =>
    [
        new
        {
            type = "function",
            function = new
            {
                name = "get_expenses",
                description = "Retrieves expenses using optional filters for status, user, and category.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        status = new { type = "string", description = "Optional status like Submitted, Approved, Draft, Rejected" },
                        userId = new { type = "integer", description = "Optional user ID" },
                        categoryId = new { type = "integer", description = "Optional category ID" }
                    }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "create_expense",
                description = "Creates a new expense record.",
                parameters = new
                {
                    type = "object",
                    required = new[] { "userId", "categoryId", "amountMinor", "expenseDate" },
                    properties = new
                    {
                        userId = new { type = "integer", description = "Employee user ID" },
                        categoryId = new { type = "integer", description = "Expense category ID" },
                        amountMinor = new { type = "integer", description = "Amount in pence, e.g. £12.34 = 1234" },
                        expenseDate = new { type = "string", description = "Date in yyyy-MM-dd" },
                        description = new { type = "string", description = "Optional description" }
                    }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "update_expense_status",
                description = "Approves, rejects, or submits an expense.",
                parameters = new
                {
                    type = "object",
                    required = new[] { "expenseId", "newStatus", "reviewedByUserId" },
                    properties = new
                    {
                        expenseId = new { type = "integer" },
                        newStatus = new { type = "string", description = "Draft, Submitted, Approved, Rejected" },
                        reviewedByUserId = new { type = "integer", description = "Manager user ID" }
                    }
                }
            }
        }
    ];

    private async Task<JsonDocument> SendChatRequestAsync(
        IEnumerable<Dictionary<string, object?>> messages,
        object[] tools,
        string endpoint,
        string deployment,
        string apiVersion,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("chat-openai");

        var managedIdentityClientId = configuration["ManagedIdentityClientId"];
        TokenCredential credential = string.IsNullOrWhiteSpace(managedIdentityClientId)
            ? new DefaultAzureCredential()
            : new ManagedIdentityCredential(managedIdentityClientId);

        var token = await credential.GetTokenAsync(new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var requestBody = JsonSerializer.Serialize(new
        {
            messages,
            tools,
            tool_choice = "auto",
            temperature = 0.2
        });

        var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
        using var response = await client.PostAsync(url, new StringContent(requestBody, Encoding.UTF8, "application/json"), cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(content);
    }
}
