using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public class ExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private SqlConnection CreateConnection()
    {
        var clientId = _configuration["ManagedIdentityClientId"];
        var connectionString = $"Server=tcp:{GetServer()},1433;Database={GetDatabase()};Authentication=Active Directory Managed Identity;User Id={clientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
        return new SqlConnection(connectionString);
    }

    private string GetServer()
    {
        var cs = _configuration.GetConnectionString("DefaultConnection") ?? "";
        var parts = cs.Split(';');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("Server=tcp:", StringComparison.OrdinalIgnoreCase))
            {
                var serverPart = trimmed.Substring("Server=tcp:".Length);
                return serverPart.Split(',')[0];
            }
        }
        return "<SQL_SERVER_FQDN>";
    }

    private string GetDatabase()
    {
        var cs = _configuration.GetConnectionString("DefaultConnection") ?? "";
        var parts = cs.Split(';');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring("Database=".Length);
            }
        }
        return "Northwind";
    }

    private void LogError(Exception ex, string operation,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        _logger.LogError(ex, "Error in {Operation} at {File}:{Line}", operation, filePath, lineNumber);
    }

    public (List<Expense> expenses, string? error) GetAllExpenses([CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_GetAllExpenses", connection) { CommandType = CommandType.StoredProcedure };
            return (ReadExpenses(command), null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetAllExpenses));
            return (GetDummyExpenses(), FormatError(ex, filePath, lineNumber));
        }
    }

    public (Expense? expense, string? error) GetExpenseById(int expenseId, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_GetExpenseById", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            var results = ReadExpenses(command);
            return (results.FirstOrDefault(), null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetExpenseById));
            return (GetDummyExpenses().First(), FormatError(ex, filePath, lineNumber));
        }
    }

    public (List<Expense> expenses, string? error) GetExpensesByUser(int userId, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_GetExpensesByUser", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@UserId", userId);
            return (ReadExpenses(command), null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetExpensesByUser));
            return (GetDummyExpenses(), FormatError(ex, filePath, lineNumber));
        }
    }

    public (List<Expense> expenses, string? error) GetExpensesByStatus(string statusName, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_GetExpensesByStatus", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@StatusName", statusName);
            return (ReadExpenses(command), null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetExpensesByStatus));
            return (GetDummyExpenses(), FormatError(ex, filePath, lineNumber));
        }
    }

    public (List<ExpenseSummary> summary, string? error) GetExpenseSummary([CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_GetExpenseSummary", connection) { CommandType = CommandType.StoredProcedure };
            var summary = new List<ExpenseSummary>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                summary.Add(new ExpenseSummary
                {
                    StatusName = reader["StatusName"]?.ToString() ?? "",
                    Count = Convert.ToInt32(reader["Count"]),
                    TotalAmountGBP = reader["TotalAmountGBP"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["TotalAmountGBP"])
                });
            }
            return (summary, null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetExpenseSummary));
            return (GetDummySummary(), FormatError(ex, filePath, lineNumber));
        }
    }

    public (bool success, string? error) CreateExpense(CreateExpenseRequest request, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            int amountMinor = (int)(request.AmountGBP * 100);
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_CreateExpense", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", amountMinor);
            command.Parameters.AddWithValue("@Currency", request.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", request.Description);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);
            command.ExecuteNonQuery();
            return (true, null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(CreateExpense));
            return (false, FormatError(ex, filePath, lineNumber));
        }
    }

    public (bool success, string? error) UpdateExpense(int expenseId, UpdateExpenseRequest request, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            int amountMinor = (int)(request.AmountGBP * 100);
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_UpdateExpense", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", amountMinor);
            command.Parameters.AddWithValue("@Currency", request.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", request.Description);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);
            command.ExecuteNonQuery();
            return (true, null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(UpdateExpense));
            return (false, FormatError(ex, filePath, lineNumber));
        }
    }

    public (bool success, string? error) DeleteExpense(int expenseId, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_DeleteExpense", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.ExecuteNonQuery();
            return (true, null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(DeleteExpense));
            return (false, FormatError(ex, filePath, lineNumber));
        }
    }

    public (bool success, string? error) SubmitExpense(int expenseId, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_SubmitExpense", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.ExecuteNonQuery();
            return (true, null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(SubmitExpense));
            return (false, FormatError(ex, filePath, lineNumber));
        }
    }

    public (bool success, string? error) ApproveExpense(int expenseId, int reviewedBy, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_ApproveExpense", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewedBy", reviewedBy);
            command.ExecuteNonQuery();
            return (true, null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(ApproveExpense));
            return (false, FormatError(ex, filePath, lineNumber));
        }
    }

    public (bool success, string? error) RejectExpense(int expenseId, int reviewedBy, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_RejectExpense", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewedBy", reviewedBy);
            command.ExecuteNonQuery();
            return (true, null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(RejectExpense));
            return (false, FormatError(ex, filePath, lineNumber));
        }
    }

    private List<Expense> ReadExpenses(SqlCommand command)
    {
        var expenses = new List<Expense>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            expenses.Add(new Expense
            {
                ExpenseId = Convert.ToInt32(reader["ExpenseId"]),
                UserName = reader["UserName"]?.ToString() ?? "",
                CategoryName = reader["CategoryName"]?.ToString() ?? "",
                StatusName = reader["StatusName"]?.ToString() ?? "",
                AmountGBP = reader["AmountGBP"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["AmountGBP"]),
                Currency = reader["Currency"]?.ToString() ?? "GBP",
                ExpenseDate = Convert.ToDateTime(reader["ExpenseDate"]),
                Description = reader["Description"]?.ToString() ?? "",
                ReceiptFile = reader["ReceiptFile"] == DBNull.Value ? null : reader["ReceiptFile"]?.ToString(),
                SubmittedAt = reader["SubmittedAt"] == DBNull.Value ? null : Convert.ToDateTime(reader["SubmittedAt"]),
                ReviewedByName = reader["ReviewedByName"] == DBNull.Value ? null : reader["ReviewedByName"]?.ToString(),
                ReviewedAt = reader["ReviewedAt"] == DBNull.Value ? null : Convert.ToDateTime(reader["ReviewedAt"]),
                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                UserId = Convert.ToInt32(reader["UserId"]),
                CategoryId = Convert.ToInt32(reader["CategoryId"]),
                StatusId = Convert.ToInt32(reader["StatusId"])
            });
        }
        return expenses;
    }

    private static string FormatError(Exception ex, string filePath, int lineNumber)
    {
        var fileName = System.IO.Path.GetFileName(filePath);
        var msg = $"Database error: {ex.Message} (at {fileName}:{lineNumber})";
        if (ex.Message.Contains("managed identity", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("Login failed", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase))
        {
            msg += " | Managed Identity Fix: Check that ManagedIdentityClientId is set in App Service configuration and the managed identity has db_datareader/db_datawriter roles on the database.";
        }
        return msg;
    }

    private static List<Expense> GetDummyExpenses() => new()
    {
        new Expense { ExpenseId = 1, UserName = "Jane Smith", CategoryName = "Travel", StatusName = "Submitted", AmountGBP = 125.50m, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-3), Description = "Train to London", CreatedAt = DateTime.Today.AddDays(-3), UserId = 1, CategoryId = 1, StatusId = 2 },
        new Expense { ExpenseId = 2, UserName = "John Doe", CategoryName = "Meals", StatusName = "Approved", AmountGBP = 45.00m, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-5), Description = "Team lunch", ReviewedByName = "Manager A", ReviewedAt = DateTime.Today.AddDays(-4), CreatedAt = DateTime.Today.AddDays(-5), UserId = 2, CategoryId = 2, StatusId = 3 },
        new Expense { ExpenseId = 3, UserName = "Alice Brown", CategoryName = "Equipment", StatusName = "Draft", AmountGBP = 299.99m, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-1), Description = "Keyboard", CreatedAt = DateTime.Today.AddDays(-1), UserId = 3, CategoryId = 3, StatusId = 1 },
        new Expense { ExpenseId = 4, UserName = "Bob Wilson", CategoryName = "Travel", StatusName = "Rejected", AmountGBP = 500.00m, Currency = "GBP", ExpenseDate = DateTime.Today.AddDays(-7), Description = "Hotel (over budget)", ReviewedByName = "Manager B", ReviewedAt = DateTime.Today.AddDays(-6), CreatedAt = DateTime.Today.AddDays(-7), UserId = 4, CategoryId = 1, StatusId = 4 },
    };

    private static List<ExpenseSummary> GetDummySummary() => new()
    {
        new ExpenseSummary { StatusName = "Draft", Count = 3, TotalAmountGBP = 750.00m },
        new ExpenseSummary { StatusName = "Submitted", Count = 5, TotalAmountGBP = 1250.50m },
        new ExpenseSummary { StatusName = "Approved", Count = 12, TotalAmountGBP = 3200.75m },
        new ExpenseSummary { StatusName = "Rejected", Count = 2, TotalAmountGBP = 600.00m },
    };
}
