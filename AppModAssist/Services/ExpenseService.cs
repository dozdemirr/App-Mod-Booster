using System.Data;
using System.Diagnostics;
using AppModAssist.Models;
using Microsoft.Data.SqlClient;

namespace AppModAssist.Services;

public sealed class ExpenseService(IConfiguration configuration, ErrorContextStore errorStore, ILogger<ExpenseService> logger) : IExpenseService
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection configuration.");

    public async Task<IReadOnlyList<ExpenseItem>> GetExpensesAsync(ExpenseFilter filter, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var cmd = new SqlCommand("dbo.usp_expenses_get", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@status", (object?)filter.Status ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@userId", (object?)filter.UserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@categoryId", (object?)filter.CategoryId ?? DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var results = new List<ExpenseItem>();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new ExpenseItem(
                    reader.GetInt32(reader.GetOrdinal("ExpenseId")),
                    reader.GetInt32(reader.GetOrdinal("UserId")),
                    reader.GetString(reader.GetOrdinal("UserName")),
                    reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    reader.GetString(reader.GetOrdinal("CategoryName")),
                    reader.GetInt32(reader.GetOrdinal("StatusId")),
                    reader.GetString(reader.GetOrdinal("StatusName")),
                    reader.GetInt32(reader.GetOrdinal("AmountMinor")),
                    reader.GetString(reader.GetOrdinal("Currency")),
                    DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("ExpenseDate"))),
                    reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                    reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
                    reader.GetDateTime(reader.GetOrdinal("CreatedAt"))));
            }

            return results;
        }
        catch (Exception ex)
        {
            var message = BuildDetailedErrorMessage(ex);
            logger.LogError(ex, "Database read failed, returning dummy data.");
            errorStore.Set(message);
            return GetDummyExpenses(filter);
        }
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand("dbo.usp_expenses_create", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@userId", request.UserId);
            cmd.Parameters.AddWithValue("@categoryId", request.CategoryId);
            cmd.Parameters.AddWithValue("@amountMinor", request.AmountMinor);
            cmd.Parameters.AddWithValue("@expenseDate", request.ExpenseDate.ToDateTime(TimeOnly.MinValue));
            cmd.Parameters.AddWithValue("@description", (object?)request.Description ?? DBNull.Value);

            var id = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(id);
        }
        catch (Exception ex)
        {
            errorStore.Set(BuildDetailedErrorMessage(ex));
            throw;
        }
    }

    public async Task UpdateExpenseStatusAsync(UpdateExpenseStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand("dbo.usp_expenses_update_status", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@expenseId", request.ExpenseId);
            cmd.Parameters.AddWithValue("@newStatus", request.NewStatus);
            cmd.Parameters.AddWithValue("@reviewedByUserId", request.ReviewedByUserId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            errorStore.Set(BuildDetailedErrorMessage(ex));
            throw;
        }
    }

    public Task<IReadOnlyList<LookupItem>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        RunLookupProcedureAsync("dbo.usp_categories_get", cancellationToken);

    public Task<IReadOnlyList<LookupItem>> GetStatusesAsync(CancellationToken cancellationToken) =>
        RunLookupProcedureAsync("dbo.usp_statuses_get", cancellationToken);

    public Task<IReadOnlyList<LookupItem>> GetUsersAsync(CancellationToken cancellationToken) =>
        RunLookupProcedureAsync("dbo.usp_users_get", cancellationToken);

    private async Task<IReadOnlyList<LookupItem>> RunLookupProcedureAsync(string procedure, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand(procedure, connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var items = new List<LookupItem>();
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new LookupItem(reader.GetInt32(0), reader.GetString(1)));
            }

            return items;
        }
        catch (Exception ex)
        {
            errorStore.Set(BuildDetailedErrorMessage(ex));
            return procedure switch
            {
                "dbo.usp_categories_get" => [new LookupItem(1, "Travel"), new LookupItem(2, "Meals"), new LookupItem(3, "Supplies")],
                "dbo.usp_statuses_get" => [new LookupItem(1, "Draft"), new LookupItem(2, "Submitted"), new LookupItem(3, "Approved"), new LookupItem(4, "Rejected")],
                _ => [new LookupItem(1, "Alice Example"), new LookupItem(2, "Bob Manager")]
            };
        }
    }

    private static string BuildDetailedErrorMessage(Exception ex)
    {
        var trace = new StackTrace(ex, true);
        var frame = trace.GetFrames()?.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.GetFileName()) && f.GetFileLineNumber() > 0);
        var file = frame?.GetFileName() ?? "unknown file";
        var line = frame?.GetFileLineNumber() ?? 0;

        var managedIdentityFix = "Managed Identity fix: ensure AZURE_CLIENT_ID is set on App Service to the user-assigned identity clientId, and ensure SQL permissions were applied via run-sql-dbrole.py.";

        return $"Database fallback mode enabled. Error: {ex.Message}. Location: {file}, line {line}. {managedIdentityFix}";
    }

    private static IReadOnlyList<ExpenseItem> GetDummyExpenses(ExpenseFilter filter)
    {
        var sample = new List<ExpenseItem>
        {
            new(1, 1, "Alice Example", 1, "Travel", 2, "Submitted", 2540, "GBP", new DateOnly(2025, 10, 20), "Taxi from airport", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-3)),
            new(2, 1, "Alice Example", 2, "Meals", 3, "Approved", 1425, "GBP", new DateOnly(2025, 9, 15), "Client lunch", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-31)),
            new(3, 1, "Alice Example", 3, "Supplies", 1, "Draft", 799, "GBP", new DateOnly(2025, 11, 1), "Stationery", null, DateTime.UtcNow.AddDays(-1))
        };

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            sample = sample.Where(x => x.StatusName.Equals(filter.Status, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (filter.CategoryId.HasValue)
        {
            sample = sample.Where(x => x.CategoryId == filter.CategoryId.Value).ToList();
        }

        if (filter.UserId.HasValue)
        {
            sample = sample.Where(x => x.UserId == filter.UserId.Value).ToList();
        }

        return sample;
    }
}
