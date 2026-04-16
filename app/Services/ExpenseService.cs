using Microsoft.Data.SqlClient;
using ExpenseMgmt.Models;
using System.Diagnostics;

namespace ExpenseMgmt.Services;

/// <summary>
/// ExpenseService: All database interactions go through stored procedures.
/// Connects to Azure SQL using Managed Identity (no username/password).
/// Returns dummy data and a detailed error message if the database is unavailable.
/// </summary>
public class ExpenseService : IExpenseService
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
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string is not configured.");
        return new SqlConnection(connectionString);
    }

    private static string FormatError(Exception ex, string methodName)
    {
        // Capture file and line from the stack trace
        var stackFrame = new StackTrace(ex, true).GetFrame(0);
        var fileName = stackFrame != null ? Path.GetFileName(stackFrame.GetFileName() ?? "ExpenseService.cs") : "ExpenseService.cs";
        var lineNumber = stackFrame?.GetFileLineNumber() ?? 0;
        var errorDetail = ex.Message;

        string managedIdentityHint = string.Empty;
        if (ex.Message.Contains("login", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("token", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("identity", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            managedIdentityHint = " | MANAGED IDENTITY FIX: Ensure (1) the user-assigned managed identity is assigned to the App Service, " +
                "(2) AZURE_CLIENT_ID app setting matches the managed identity client ID, " +
                "(3) the managed identity has been added as a database user via run-sql-dbrole.py (CREATE USER ... FROM EXTERNAL PROVIDER), " +
                "(4) for local development use 'Authentication=Active Directory Default' in appsettings.Development.json and run 'az login'.";
        }

        return $"Database error in {fileName}, method '{methodName}', line {lineNumber}: {errorDetail}.{managedIdentityHint}";
    }

    // ─── GetExpenses ────────────────────────────────────────────────────
    public async Task<(List<Expense> Expenses, string? ErrorMessage)> GetExpensesAsync(string? filter = null, int? userId = null)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpenses", conn) { CommandType = System.Data.CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@Filter", (object?)filter ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
            var results = await ReadExpensesAsync(cmd);
            return (results, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetExpensesAsync");
            return (GetDummyExpenses(), FormatError(ex, nameof(GetExpensesAsync)));
        }
    }

    // ─── GetExpenseById ──────────────────────────────────────────────────
    public async Task<(Expense? Expense, string? ErrorMessage)> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpenseById", conn) { CommandType = System.Data.CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            var results = await ReadExpensesAsync(cmd);
            return (results.FirstOrDefault(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetExpenseByIdAsync");
            return (null, FormatError(ex, nameof(GetExpenseByIdAsync)));
        }
    }

    // ─── CreateExpense ───────────────────────────────────────────────────
    public async Task<(int ExpenseId, string? ErrorMessage)> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_CreateExpense", conn) { CommandType = System.Data.CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", request.UserId);
            cmd.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            cmd.Parameters.AddWithValue("@AmountMinor", (int)(request.AmountGBP * 100));
            cmd.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate.Date);
            cmd.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);
            var outputParam = new SqlParameter("@ExpenseId", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(outputParam);
            await cmd.ExecuteNonQueryAsync();
            return ((int)outputParam.Value, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateExpenseAsync");
            return (-1, FormatError(ex, nameof(CreateExpenseAsync)));
        }
    }

    // ─── SubmitExpense ───────────────────────────────────────────────────
    public async Task<(bool Success, string? ErrorMessage)> SubmitExpenseAsync(int expenseId, int userId)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_SubmitExpense", conn) { CommandType = System.Data.CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SubmitExpenseAsync");
            return (false, FormatError(ex, nameof(SubmitExpenseAsync)));
        }
    }

    // ─── ApproveExpense ──────────────────────────────────────────────────
    public async Task<(bool Success, string? ErrorMessage)> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_ApproveExpense", conn) { CommandType = System.Data.CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            cmd.Parameters.AddWithValue("@ReviewerId", reviewerId);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ApproveExpenseAsync");
            return (false, FormatError(ex, nameof(ApproveExpenseAsync)));
        }
    }

    // ─── RejectExpense ───────────────────────────────────────────────────
    public async Task<(bool Success, string? ErrorMessage)> RejectExpenseAsync(int expenseId, int reviewerId, string? rejectionReason)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_RejectExpense", conn) { CommandType = System.Data.CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            cmd.Parameters.AddWithValue("@ReviewerId", reviewerId);
            cmd.Parameters.AddWithValue("@RejectionReason", (object?)rejectionReason ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RejectExpenseAsync");
            return (false, FormatError(ex, nameof(RejectExpenseAsync)));
        }
    }

    // ─── GetPendingExpenses ──────────────────────────────────────────────
    public async Task<(List<Expense> Expenses, string? ErrorMessage)> GetPendingExpensesAsync(string? filter = null)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetPendingExpenses", conn) { CommandType = System.Data.CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@Filter", (object?)filter ?? DBNull.Value);
            var results = await ReadExpensesAsync(cmd);
            return (results, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPendingExpensesAsync");
            return (GetDummyPendingExpenses(), FormatError(ex, nameof(GetPendingExpensesAsync)));
        }
    }

    // ─── GetCategories ───────────────────────────────────────────────────
    public async Task<(List<ExpenseCategory> Categories, string? ErrorMessage)> GetCategoriesAsync()
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetCategories", conn) { CommandType = System.Data.CommandType.StoredProcedure };
            using var reader = await cmd.ExecuteReaderAsync();
            var categories = new List<ExpenseCategory>();
            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName"))
                });
            }
            return (categories, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetCategoriesAsync");
            return (GetDummyCategories(), FormatError(ex, nameof(GetCategoriesAsync)));
        }
    }

    // ─── GetUsers ────────────────────────────────────────────────────────
    public async Task<(List<User> Users, string? ErrorMessage)> GetUsersAsync()
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetUsers", conn) { CommandType = System.Data.CommandType.StoredProcedure };
            using var reader = await cmd.ExecuteReaderAsync();
            var users = new List<User>();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                    ManagerId = reader.IsDBNull(reader.GetOrdinal("ManagerId")) ? null : reader.GetInt32(reader.GetOrdinal("ManagerId")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
            return (users, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUsersAsync");
            return (GetDummyUsers(), FormatError(ex, nameof(GetUsersAsync)));
        }
    }

    // ─── GetExpensesByUserId ─────────────────────────────────────────────
    public async Task<(List<Expense> Expenses, string? ErrorMessage)> GetExpensesByUserIdAsync(int userId, string? filter = null)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpensesByUserId", conn) { CommandType = System.Data.CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Filter", (object?)filter ?? DBNull.Value);
            var results = await ReadExpensesAsync(cmd);
            return (results, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetExpensesByUserIdAsync");
            return (GetDummyExpenses(), FormatError(ex, nameof(GetExpensesByUserIdAsync)));
        }
    }

    // ─── DeleteExpense ───────────────────────────────────────────────────
    public async Task<(bool Success, string? ErrorMessage)> DeleteExpenseAsync(int expenseId, int userId)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_DeleteExpense", conn) { CommandType = System.Data.CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await cmd.ExecuteNonQueryAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteExpenseAsync");
            return (false, FormatError(ex, nameof(DeleteExpenseAsync)));
        }
    }

    // ─── GetExpenseSummary ───────────────────────────────────────────────
    public async Task<(List<ExpenseSummary> Summary, string? ErrorMessage)> GetExpenseSummaryAsync(int? userId = null)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpenseSummary", conn) { CommandType = System.Data.CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
            using var reader = await cmd.ExecuteReaderAsync();
            var summary = new List<ExpenseSummary>();
            while (await reader.ReadAsync())
            {
                summary.Add(new ExpenseSummary
                {
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                    ExpenseCount = reader.GetInt32(reader.GetOrdinal("ExpenseCount")),
                    TotalAmountGBP = reader.GetDecimal(reader.GetOrdinal("TotalAmountGBP"))
                });
            }
            return (summary, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetExpenseSummaryAsync");
            return (GetDummySummary(), FormatError(ex, nameof(GetExpenseSummaryAsync)));
        }
    }

    // ─── Helper: Read Expenses from reader ──────────────────────────────
    private static async Task<List<Expense>> ReadExpensesAsync(SqlCommand cmd)
    {
        using var reader = await cmd.ExecuteReaderAsync();
        var expenses = new List<Expense>();
        while (await reader.ReadAsync())
        {
            expenses.Add(MapExpense(reader));
        }
        return expenses;
    }

    private static Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            AmountGBP = reader.GetDecimal(reader.GetOrdinal("AmountGBP")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    // ─── Dummy Data (returned on DB connection failure) ──────────────────
    private static List<Expense> GetDummyExpenses() => new()
    {
        new Expense { ExpenseId = 1, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", AmountMinor = 12000, AmountGBP = 120.00m, Currency = "GBP", ExpenseDate = new DateTime(2024, 1, 15), Description = "Taxi from airport to client site (DEMO DATA - DB unavailable)", CreatedAt = DateTime.UtcNow },
        new Expense { ExpenseId = 2, UserId = 1, UserName = "Alice Example", CategoryId = 2, CategoryName = "Meals", StatusId = 3, StatusName = "Approved", AmountMinor = 6900, AmountGBP = 69.00m, Currency = "GBP", ExpenseDate = new DateTime(2023, 1, 10), Description = "Client lunch meeting (DEMO DATA - DB unavailable)", CreatedAt = DateTime.UtcNow },
        new Expense { ExpenseId = 3, UserId = 1, UserName = "Alice Example", CategoryId = 3, CategoryName = "Supplies", StatusId = 3, StatusName = "Approved", AmountMinor = 9950, AmountGBP = 99.50m, Currency = "GBP", ExpenseDate = new DateTime(2023, 12, 4), Description = "Office supplies (DEMO DATA - DB unavailable)", CreatedAt = DateTime.UtcNow },
        new Expense { ExpenseId = 4, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 4, StatusName = "Rejected", AmountMinor = 1920, AmountGBP = 19.20m, Currency = "GBP", ExpenseDate = new DateTime(2023, 12, 18), Description = "Bus fare (DEMO DATA - DB unavailable)", CreatedAt = DateTime.UtcNow }
    };

    private static List<Expense> GetDummyPendingExpenses() => new()
    {
        new Expense { ExpenseId = 1, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", AmountMinor = 12000, AmountGBP = 120.00m, Currency = "GBP", ExpenseDate = new DateTime(2024, 1, 20), Description = "Taxi to client office (DEMO DATA - DB unavailable)", SubmittedAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow },
        new Expense { ExpenseId = 3, UserId = 1, UserName = "Alice Example", CategoryId = 3, CategoryName = "Supplies", StatusId = 2, StatusName = "Submitted", AmountMinor = 9950, AmountGBP = 99.50m, Currency = "GBP", ExpenseDate = new DateTime(2023, 12, 14), Description = "Office supplies for project (DEMO DATA - DB unavailable)", SubmittedAt = DateTime.UtcNow.AddDays(-3), CreatedAt = DateTime.UtcNow }
    };

    private static List<ExpenseCategory> GetDummyCategories() => new()
    {
        new ExpenseCategory { CategoryId = 1, CategoryName = "Travel" },
        new ExpenseCategory { CategoryId = 2, CategoryName = "Meals" },
        new ExpenseCategory { CategoryId = 3, CategoryName = "Supplies" },
        new ExpenseCategory { CategoryId = 4, CategoryName = "Accommodation" },
        new ExpenseCategory { CategoryId = 5, CategoryName = "Other" }
    };

    private static List<User> GetDummyUsers() => new()
    {
        new User { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleName = "Employee", IsActive = true },
        new User { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleName = "Manager", IsActive = true }
    };

    private static List<ExpenseSummary> GetDummySummary() => new()
    {
        new ExpenseSummary { StatusName = "Draft", ExpenseCount = 1, TotalAmountGBP = 7.99m },
        new ExpenseSummary { StatusName = "Submitted", ExpenseCount = 1, TotalAmountGBP = 25.40m },
        new ExpenseSummary { StatusName = "Approved", ExpenseCount = 2, TotalAmountGBP = 137.25m }
    };
}
