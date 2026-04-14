using Azure.Core;
using Azure.Identity;
using ModernExpenseApp.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModernExpenseApp.Services;

public sealed class ChatService(
    IConfiguration configuration,
    IExpenseService expenseService,
    IHttpClientFactory httpClientFactory,
    ILogger<ChatService> logger,
    IWebHostEnvironment environment) : IChatService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> AskAsync(string userMessage, CancellationToken cancellationToken)
    {
        var endpoint = configuration["OpenAI:Endpoint"];
        var deploymentName = configuration["OpenAI:DeploymentName"] ?? "gpt-4o";

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "GenAI resources are not configured. Deploy with bash deploy-with-chat.sh to enable Azure OpenAI chat.";
        }

        var credential = GetCredential();
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]),
            cancellationToken);

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var tools = BuildTools();
        var ragContext = LoadRagContext();
        var messages = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "system",
                ["content"] = "You are an expense assistant. Use tools for real data operations. " +
                              "Capabilities: list expenses, create expense, review expense, list users and categories. " +
                              "Use tools whenever user requests database data changes or reads. " +
                              $"Retrieved context: {ragContext}"
            },
            new JsonObject { ["role"] = "user", ["content"] = userMessage }
        };

        for (var iteration = 0; iteration < 4; iteration++)
        {
            var request = new JsonObject
            {
                ["messages"] = messages,
                ["tools"] = tools,
                ["tool_choice"] = "auto",
                ["temperature"] = 0.2
            };

            var response = await client.PostAsync(
                $"{endpoint.TrimEnd('/')}/openai/deployments/{deploymentName}/chat/completions?api-version=2024-06-01",
                new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json"),
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var root = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var message = root?["choices"]?[0]?["message"];
            if (message is null)
            {
                return "I could not get a valid chat response.";
            }

            var toolCalls = message["tool_calls"]?.AsArray();
            if (toolCalls is null || toolCalls.Count == 0)
            {
                return message["content"]?.ToString() ?? "No response content returned.";
            }

            messages.Add(new JsonObject
            {
                ["role"] = "assistant",
                ["tool_calls"] = toolCalls
            });

            foreach (var toolCall in toolCalls)
            {
                var id = toolCall?["id"]?.ToString() ?? Guid.NewGuid().ToString("N");
                var functionName = toolCall?["function"]?["name"]?.ToString();
                var args = toolCall?["function"]?["arguments"]?.ToString() ?? "{}";
                var result = await ExecuteToolAsync(functionName, args, cancellationToken);

                messages.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = id,
                    ["content"] = result
                });
            }
        }

        return "I could not finish all tool calls. Please try again.";
    }

    private static JsonArray BuildTools() =>
    [
        CreateFunction("list_expenses", "Gets expenses with optional status and user filters.",
            """{"type":"object","properties":{"statusId":{"type":"integer"},"userId":{"type":"integer"}}}"""),
        CreateFunction("create_expense", "Creates a new expense item.",
            """{"type":"object","properties":{"userId":{"type":"integer"},"categoryId":{"type":"integer"},"amountMinor":{"type":"integer"},"expenseDate":{"type":"string","description":"YYYY-MM-DD"},"description":{"type":"string"}},"required":["userId","categoryId","amountMinor","expenseDate"]}"""),
        CreateFunction("review_expense", "Approves or rejects an expense as a manager.",
            """{"type":"object","properties":{"expenseId":{"type":"integer"},"managerUserId":{"type":"integer"},"approve":{"type":"boolean"}},"required":["expenseId","managerUserId","approve"]}"""),
        CreateFunction("list_users", "Lists all users in the system.", """{"type":"object","properties":{}}"""),
        CreateFunction("list_categories", "Lists expense categories.", """{"type":"object","properties":{}}""")
    ];

    private static JsonObject CreateFunction(string name, string description, string schema) => new()
    {
        ["type"] = "function",
        ["function"] = new JsonObject
        {
            ["name"] = name,
            ["description"] = description,
            ["parameters"] = JsonNode.Parse(schema)
        }
    };

    private async Task<string> ExecuteToolAsync(string? functionName, string argsJson, CancellationToken cancellationToken)
    {
        try
        {
            var args = JsonNode.Parse(argsJson)?.AsObject() ?? new JsonObject();
            switch (functionName)
            {
                case "list_expenses":
                {
                    int? statusId = args["statusId"]?.GetValue<int>();
                    int? userId = args["userId"]?.GetValue<int>();
                    var data = await expenseService.GetExpensesAsync(statusId, userId, cancellationToken);
                    return JsonSerializer.Serialize(data, JsonOptions);
                }
                case "create_expense":
                {
                    var request = new CreateExpenseRequest
                    {
                        UserId = args["userId"]?.GetValue<int>() ?? 0,
                        CategoryId = args["categoryId"]?.GetValue<int>() ?? 0,
                        AmountMinor = args["amountMinor"]?.GetValue<int>() ?? 0,
                        ExpenseDate = DateOnly.Parse(args["expenseDate"]?.ToString() ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")),
                        Description = args["description"]?.ToString()
                    };
                    var createdId = await expenseService.CreateExpenseAsync(request, cancellationToken);
                    return JsonSerializer.Serialize(new { createdId }, JsonOptions);
                }
                case "review_expense":
                {
                    var expenseId = args["expenseId"]?.GetValue<int>() ?? 0;
                    var managerUserId = args["managerUserId"]?.GetValue<int>() ?? 0;
                    var approve = args["approve"]?.GetValue<bool>() ?? false;
                    await expenseService.ReviewExpenseAsync(expenseId, managerUserId, approve, cancellationToken);
                    return JsonSerializer.Serialize(new { success = true }, JsonOptions);
                }
                case "list_users":
                {
                    var users = await expenseService.GetUsersAsync(cancellationToken);
                    return JsonSerializer.Serialize(users, JsonOptions);
                }
                case "list_categories":
                {
                    var categories = await expenseService.GetCategoriesAsync(cancellationToken);
                    return JsonSerializer.Serialize(categories, JsonOptions);
                }
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown function '{functionName}'." }, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tool execution failed for {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    private TokenCredential GetCredential()
    {
        var managedIdentityClientId = configuration["ManagedIdentityClientId"] ?? configuration["AZURE_CLIENT_ID"];
        if (!string.IsNullOrWhiteSpace(managedIdentityClientId))
        {
            logger.LogInformation("Using ManagedIdentityCredential with client ID.");
            return new ManagedIdentityCredential(managedIdentityClientId);
        }

        logger.LogInformation("Using DefaultAzureCredential.");
        return new DefaultAzureCredential();
    }

    private string LoadRagContext()
    {
        try
        {
            var repoRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."));
            var ragPath = Path.Combine(repoRoot, "RAG");
            if (!Directory.Exists(ragPath))
            {
                return "No RAG context available.";
            }

            var snippets = Directory.GetFiles(ragPath, "*.md")
                .Select(path => File.ReadAllText(path))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Take(3);

            return string.Join(" ", snippets);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load RAG context.");
            return "RAG context load failed.";
        }
    }
}
