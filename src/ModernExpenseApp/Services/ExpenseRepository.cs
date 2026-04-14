using Microsoft.Data.SqlClient;
using ModernExpenseApp.Models;
using System.Data;

namespace ModernExpenseApp.Services;

public sealed class ExpenseRepository(IConfiguration configuration, IWebHostEnvironment environment) : IExpenseRepository
{
    public async Task<IReadOnlyList<ExpenseDto>> GetExpensesAsync(int? statusId, int? userId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.sp_get_expenses", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@StatusId", (object?)statusId ?? DBNull.Value);
        command.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);

        var result = new List<ExpenseDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ExpenseDto
            {
                ExpenseId = reader.GetInt32("ExpenseId"),
                UserId = reader.GetInt32("UserId"),
                UserName = reader.GetString("UserName"),
                CategoryId = reader.GetInt32("CategoryId"),
                CategoryName = reader.GetString("CategoryName"),
                StatusId = reader.GetInt32("StatusId"),
                StatusName = reader.GetString("StatusName"),
                AmountMinor = reader.GetInt32("AmountMinor"),
                Currency = reader.GetString("Currency"),
                ExpenseDate = DateOnly.FromDateTime(reader.GetDateTime("ExpenseDate")),
                Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                SubmittedAt = reader.IsDBNull("SubmittedAt") ? null : reader.GetDateTime("SubmittedAt"),
                ReviewedAt = reader.IsDBNull("ReviewedAt") ? null : reader.GetDateTime("ReviewedAt")
            });
        }

        return result;
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.sp_create_expense", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@UserId", request.UserId);
        command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
        command.Parameters.AddWithValue("@AmountMinor", request.AmountMinor);
        command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate.ToDateTime(TimeOnly.MinValue));
        command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);

        var createdId = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return createdId;
    }

    public async Task ReviewExpenseAsync(int expenseId, int managerUserId, bool approve, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.sp_review_expense", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@ExpenseId", expenseId);
        command.Parameters.AddWithValue("@ManagerUserId", managerUserId);
        command.Parameters.AddWithValue("@Approve", approve);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.sp_get_users", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var result = new List<UserDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new UserDto
            {
                UserId = reader.GetInt32("UserId"),
                UserName = reader.GetString("UserName"),
                Email = reader.GetString("Email"),
                RoleName = reader.GetString("RoleName")
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.sp_get_categories", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var result = new List<CategoryDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CategoryDto
            {
                CategoryId = reader.GetInt32("CategoryId"),
                CategoryName = reader.GetString("CategoryName")
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<StatusDto>> GetStatusesAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("dbo.sp_get_statuses", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var result = new List<StatusDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new StatusDto
            {
                StatusId = reader.GetInt32("StatusId"),
                StatusName = reader.GetString("StatusName")
            });
        }

        return result;
    }

    private string GetConnectionString()
    {
        var key = environment.IsDevelopment() ? "SqlDbLocal" : "SqlDb";
        return configuration.GetConnectionString(key)
               ?? configuration.GetConnectionString("SqlDb")
               ?? throw new InvalidOperationException("Connection string 'SqlDb' is missing.");
    }
}
