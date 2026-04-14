using System.Net.Http.Json;
using AppModAssist.Models;

namespace AppModAssist.Services;

public class ExpenseApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ExpenseApiClient(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ApiResponse<DashboardData>?> GetDashboardAsync(int? userId, int? categoryId, int? statusId, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var query = $"api/expenses?userId={userId}&categoryId={categoryId}&statusId={statusId}";
        return await client.GetFromJsonAsync<ApiResponse<DashboardData>>(query, cancellationToken);
    }

    public async Task<ApiResponse<ExpenseItem>?> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("api/expenses", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse<ExpenseItem>>(cancellationToken: cancellationToken);
    }

    public async Task<ApiResponse<bool>?> SubmitExpenseAsync(int expenseId, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("api/expenses/submit", new SubmitExpenseRequest(expenseId), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(cancellationToken: cancellationToken);
    }

    public async Task<ApiResponse<bool>?> ReviewExpenseAsync(ReviewExpenseRequest request, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("api/expenses/review", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(cancellationToken: cancellationToken);
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request is null)
        {
            throw new InvalidOperationException("No current HTTP request.");
        }

        client.BaseAddress = new Uri($"{request.Scheme}://{request.Host}/");
        return client;
    }
}
