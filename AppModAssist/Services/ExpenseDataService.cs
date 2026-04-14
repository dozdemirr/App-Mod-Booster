using AppModAssist.Models;
using Microsoft.Data.SqlClient;

namespace AppModAssist.Services;

public class ExpenseDataService : IExpenseDataService
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly ILogger<ExpenseDataService> _logger;

    public ExpenseDataService(SqlConnectionFactory connectionFactory, ILogger<ExpenseDataService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<ApiResponse<DashboardData>> GetDashboardAsync(int? userId, int? categoryId, int? statusId, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync(cancellationToken);

            var expenses = await GetExpensesAsync(connection, userId, categoryId, statusId, cancellationToken);
            var users = await GetLookupAsync(connection, "usp_get_lookup_users", cancellationToken);
            var categories = await GetLookupAsync(connection, "usp_get_lookup_categories", cancellationToken);
            var statuses = await GetLookupAsync(connection, "usp_get_lookup_statuses", cancellationToken);

            return new ApiResponse<DashboardData>
            {
                Data = new DashboardData(expenses, users, categories, statuses),
                UsedFallback = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading dashboard.");
            return new ApiResponse<DashboardData>
            {
                Data = BuildFallbackDashboard(),
                UsedFallback = true,
                ErrorBanner = ErrorBannerFactory.Create(ex)
            };
        }
    }

    public async Task<ApiResponse<ExpenseItem>> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("usp_create_expense", connection) { CommandType = System.Data.CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", Decimal.ToInt32(request.AmountGbp * 100));
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate.ToDateTime(TimeOnly.MinValue));
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);
            var expenseId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

            var current = await GetExpensesAsync(connection, null, null, null, cancellationToken);
            var created = current.First(x => x.ExpenseId == expenseId);
            return new ApiResponse<ExpenseItem> { Data = created, UsedFallback = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed creating expense.");
            var fallback = BuildFallbackDashboard().Expenses.First();
            return new ApiResponse<ExpenseItem> { Data = fallback, UsedFallback = true, ErrorBanner = ErrorBannerFactory.Create(ex) };
        }
    }

    public async Task<ApiResponse<bool>> SubmitExpenseAsync(SubmitExpenseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("usp_submit_expense", connection) { CommandType = System.Data.CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@ExpenseId", request.ExpenseId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new ApiResponse<bool> { Data = true, UsedFallback = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed submitting expense.");
            return new ApiResponse<bool> { Data = false, UsedFallback = true, ErrorBanner = ErrorBannerFactory.Create(ex) };
        }
    }

    public async Task<ApiResponse<bool>> ReviewExpenseAsync(ReviewExpenseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("usp_review_expense", connection) { CommandType = System.Data.CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@ExpenseId", request.ExpenseId);
            command.Parameters.AddWithValue("@ReviewedByUserId", request.ReviewedByUserId);
            command.Parameters.AddWithValue("@Approve", request.Approve);
            command.Parameters.AddWithValue("@Notes", (object?)request.Notes ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new ApiResponse<bool> { Data = true, UsedFallback = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed reviewing expense.");
            return new ApiResponse<bool> { Data = false, UsedFallback = true, ErrorBanner = ErrorBannerFactory.Create(ex) };
        }
    }

    private static async Task<IReadOnlyList<ExpenseItem>> GetExpensesAsync(SqlConnection connection, int? userId, int? categoryId, int? statusId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("usp_get_expenses", connection) { CommandType = System.Data.CommandType.StoredProcedure };
        command.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
        command.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("@StatusId", (object?)statusId ?? DBNull.Value);

        var output = new List<ExpenseItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            output.Add(new ExpenseItem(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetDecimal(5),
                DateOnly.FromDateTime(reader.GetDateTime(6)),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : new DateTimeOffset(reader.GetDateTime(9)),
                reader.IsDBNull(10) ? null : new DateTimeOffset(reader.GetDateTime(10))));
        }

        return output;
    }

    private static async Task<IReadOnlyList<LookupItem>> GetLookupAsync(SqlConnection connection, string storedProcedure, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(storedProcedure, connection) { CommandType = System.Data.CommandType.StoredProcedure };
        var output = new List<LookupItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            output.Add(new LookupItem(reader.GetInt32(0), reader.GetString(1)));
        }

        return output;
    }

    private static DashboardData BuildFallbackDashboard()
    {
        return new DashboardData(
        [
            new ExpenseItem(101, "Fallback User", "fallback@example.com", "Travel", "Submitted", 25.40m, DateOnly.FromDateTime(DateTime.UtcNow.Date), "Fallback taxi expense", null, DateTimeOffset.UtcNow, null),
            new ExpenseItem(102, "Fallback User", "fallback@example.com", "Meals", "Draft", 9.99m, DateOnly.FromDateTime(DateTime.UtcNow.Date), "Fallback meal expense", null, null, null)
        ],
        [new LookupItem(1, "Alice Example"), new LookupItem(2, "Bob Manager")],
        [new LookupItem(1, "Travel"), new LookupItem(2, "Meals"), new LookupItem(3, "Supplies")],
        [new LookupItem(1, "Draft"), new LookupItem(2, "Submitted"), new LookupItem(3, "Approved"), new LookupItem(4, "Rejected")]);
    }
}
