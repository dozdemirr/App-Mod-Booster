using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AppModAssist.Models;
using Azure.Core;
using Azure.Identity;

namespace AppModAssist.Services;

public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ExpenseApiClient _apiClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatService> _logger;
    private readonly IWebHostEnvironment _environment;

    public ChatService(IConfiguration configuration, ExpenseApiClient apiClient, IHttpClientFactory httpClientFactory, ILogger<ChatService> logger, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _apiClient = apiClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _environment = environment;
    }

    public async Task<ChatResponse> AskAsync(string userMessage, CancellationToken cancellationToken)
    {
        var endpoint = _configuration["OpenAI:Endpoint"];
        var deployment = _configuration["OpenAI:DeploymentName"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            return new ChatResponse(
                "GenAI is not deployed. I can still help with guidance. Run `bash deploy-with-chat.sh` to enable AI responses.",
                true,
                "Deploy Azure OpenAI and AI Search using deploy-with-chat.sh.");
        }

        try
        {
            var systemPrompt = """
                You are the expense assistant. You can call backend functions to read and update expenses.
                Available capabilities:
                - list expenses with optional filters
                - create an expense
                - submit an expense
                - approve or reject an expense
                Prefer calling tools for data operations, then summarize clearly.
                """;
            var ragContext = await LoadRagContextAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(ragContext))
            {
                systemPrompt += $"\nContext:\n{ragContext}";
            }

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            };

            for (var i = 0; i < 4; i++)
            {
                var completion = await CompleteAsync(endpoint, deployment, messages, cancellationToken);
                var choice = completion.RootElement.GetProperty("choices")[0].GetProperty("message");
                if (!choice.TryGetProperty("tool_calls", out var toolCalls))
                {
                    var content = choice.TryGetProperty("content", out var c) ? c.GetString() : "No response.";
                    return new ChatResponse(content ?? "No response.", false, null);
                }

                messages.Add(new
                {
                    role = "assistant",
                    tool_calls = JsonSerializer.Deserialize<object>(toolCalls.GetRawText())
                });

                foreach (var tool in toolCalls.EnumerateArray())
                {
                    var toolCallId = tool.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
                    var function = tool.GetProperty("function");
                    var functionName = function.GetProperty("name").GetString() ?? string.Empty;
                    var arguments = function.GetProperty("arguments").GetString() ?? "{}";
                    var toolResult = await ExecuteToolAsync(functionName, arguments, cancellationToken);
                    messages.Add(new { role = "tool", tool_call_id = toolCallId, content = toolResult });
                }
            }

            return new ChatResponse("I couldn't finish that request. Please try a simpler prompt.", true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat orchestration failure.");
            return new ChatResponse($"Chat error: {ex.Message}", true, "Verify OpenAI__Endpoint/OpenAI__DeploymentName and managed identity role assignments.");
        }
    }

    private async Task<string> ExecuteToolAsync(string functionName, string rawArgs, CancellationToken cancellationToken)
    {
        var args = JsonDocument.Parse(rawArgs).RootElement;
        return functionName switch
        {
            "get_expenses" => await GetExpensesAsync(args, cancellationToken),
            "create_expense" => await CreateExpenseAsync(args, cancellationToken),
            "submit_expense" => await SubmitExpenseAsync(args, cancellationToken),
            "review_expense" => await ReviewExpenseAsync(args, cancellationToken),
            _ => JsonSerializer.Serialize(new { error = $"Unknown function '{functionName}'." })
        };
    }

    private async Task<string> GetExpensesAsync(JsonElement args, CancellationToken cancellationToken)
    {
        int? userId = args.TryGetProperty("userId", out var user) ? user.GetInt32() : null;
        int? categoryId = args.TryGetProperty("categoryId", out var category) ? category.GetInt32() : null;
        int? statusId = args.TryGetProperty("statusId", out var status) ? status.GetInt32() : null;
        var result = await _apiClient.GetDashboardAsync(userId, categoryId, statusId, cancellationToken);
        return JsonSerializer.Serialize(result?.Data.Expenses ?? Array.Empty<ExpenseItem>());
    }

    private async Task<string> CreateExpenseAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var request = new CreateExpenseRequest(
            args.GetProperty("userId").GetInt32(),
            args.GetProperty("categoryId").GetInt32(),
            args.GetProperty("amountGbp").GetDecimal(),
            DateOnly.Parse(args.GetProperty("expenseDate").GetString()!),
            args.TryGetProperty("description", out var description) ? description.GetString() : null,
            args.TryGetProperty("receiptFile", out var receipt) ? receipt.GetString() : null);

        var result = await _apiClient.CreateExpenseAsync(request, cancellationToken);
        return JsonSerializer.Serialize(result?.Data);
    }

    private async Task<string> SubmitExpenseAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var result = await _apiClient.SubmitExpenseAsync(args.GetProperty("expenseId").GetInt32(), cancellationToken);
        return JsonSerializer.Serialize(result?.Data);
    }

    private async Task<string> ReviewExpenseAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var request = new ReviewExpenseRequest(
            args.GetProperty("expenseId").GetInt32(),
            args.GetProperty("reviewedByUserId").GetInt32(),
            args.GetProperty("approve").GetBoolean(),
            args.TryGetProperty("notes", out var notes) ? notes.GetString() : null);

        var result = await _apiClient.ReviewExpenseAsync(request, cancellationToken);
        return JsonSerializer.Serialize(result?.Data);
    }

    private async Task<JsonDocument> CompleteAsync(string endpoint, string deployment, IReadOnlyList<object> messages, CancellationToken cancellationToken)
    {
        var tools = new object[]
        {
            new
            {
                type = "function",
                function = new
                {
                    name = "get_expenses",
                    description = "Get expenses with optional filters.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            userId = new { type = "integer", description = "Optional user id." },
                            categoryId = new { type = "integer", description = "Optional category id." },
                            statusId = new { type = "integer", description = "Optional status id." }
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
                    description = "Create a new expense record.",
                    parameters = new
                    {
                        type = "object",
                        required = new[] { "userId", "categoryId", "amountGbp", "expenseDate" },
                        properties = new
                        {
                            userId = new { type = "integer" },
                            categoryId = new { type = "integer" },
                            amountGbp = new { type = "number" },
                            expenseDate = new { type = "string", description = "Date in yyyy-MM-dd format." },
                            description = new { type = "string" },
                            receiptFile = new { type = "string" }
                        }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "submit_expense",
                    description = "Submit an expense for approval.",
                    parameters = new
                    {
                        type = "object",
                        required = new[] { "expenseId" },
                        properties = new { expenseId = new { type = "integer" } }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "review_expense",
                    description = "Approve or reject an expense.",
                    parameters = new
                    {
                        type = "object",
                        required = new[] { "expenseId", "reviewedByUserId", "approve" },
                        properties = new
                        {
                            expenseId = new { type = "integer" },
                            reviewedByUserId = new { type = "integer" },
                            approve = new { type = "boolean" },
                            notes = new { type = "string" }
                        }
                    }
                }
            }
        };

        var payload = new
        {
            messages,
            tools,
            tool_choice = "auto",
            temperature = 0.2
        };

        var credential = BuildCredential();
        var token = await credential.GetTokenAsync(new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), cancellationToken);
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        var request = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version=2024-10-21";
        var response = await client.PostAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(content);
    }

    private TokenCredential BuildCredential()
    {
        var managedIdentityClientId = _configuration["ManagedIdentityClientId"] ?? _configuration["AZURE_CLIENT_ID"];
        if (!string.IsNullOrWhiteSpace(managedIdentityClientId))
        {
            _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
            return new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId));
        }

        _logger.LogInformation("Using DefaultAzureCredential");
        return new DefaultAzureCredential();
    }

    private async Task<string?> LoadRagContextAsync(CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "RAG", "context.md"));
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }
}
