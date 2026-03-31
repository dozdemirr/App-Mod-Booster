using Microsoft.Data.SqlClient;
using ExpenseApp.Models;
using System.Data;

namespace ExpenseApp.Services;

public class ExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;
    private string? _lastError;
    private string? _lastErrorFile;
    private int? _lastErrorLine;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string? LastError => _lastError;
    public string? LastErrorFile => _lastErrorFile;
    public int? LastErrorLine => _lastErrorLine;

    private SqlConnection GetConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        return new SqlConnection(connectionString);
    }

    private void SetError(Exception ex, string file, int line)
    {
        _lastError = ex.Message;
        _lastErrorFile = file;
        _lastErrorLine = line;
        _logger.LogError(ex, "Database error at {File}:{Line}", file, line);
    }

    public async Task<List<Expense>> GetAllExpensesAsync()
    {
        try
        {
            _lastError = null;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetAllExpenses", conn) { CommandType = CommandType.StoredProcedure };
            return await ReadExpenses(cmd);
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 48);
            return GetDummyExpenses();
        }
    }

    public async Task<List<Expense>> GetExpensesByStatusAsync(string statusName)
    {
        try
        {
            _lastError = null;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpensesByStatus", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@StatusName", statusName);
            return await ReadExpenses(cmd);
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 63);
            return GetDummyExpenses().Where(e => e.StatusName == statusName).ToList();
        }
    }

    public async Task<List<Expense>> GetExpensesByUserAsync(int userId)
    {
        try
        {
            _lastError = null;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpensesByUser", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", userId);
            return await ReadExpenses(cmd);
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 79);
            return GetDummyExpenses().Where(e => e.UserId == userId).ToList();
        }
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            _lastError = null;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpenseById", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            var expenses = await ReadExpenses(cmd);
            return expenses.FirstOrDefault();
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 96);
            return GetDummyExpenses().FirstOrDefault(e => e.ExpenseId == expenseId);
        }
    }

    public async Task<int?> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            _lastError = null;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_CreateExpense", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@UserId", request.UserId);
            cmd.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            cmd.Parameters.AddWithValue("@AmountMinor", (int)(request.AmountGBP * 100));
            cmd.Parameters.AddWithValue("@Currency", request.Currency);
            cmd.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            cmd.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);
            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : null;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 119);
            return null;
        }
    }

    public async Task<bool> UpdateExpenseStatusAsync(UpdateExpenseStatusRequest request)
    {
        try
        {
            _lastError = null;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_UpdateExpenseStatus", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ExpenseId", request.ExpenseId);
            cmd.Parameters.AddWithValue("@StatusName", request.StatusName);
            cmd.Parameters.AddWithValue("@ReviewedBy", request.ReviewedBy);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 136);
            return false;
        }
    }

    public async Task<bool> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            _lastError = null;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_SubmitExpense", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 153);
            return false;
        }
    }

    public async Task<List<ExpenseUser>> GetAllUsersAsync()
    {
        try
        {
            _lastError = null;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetAllUsers", conn) { CommandType = CommandType.StoredProcedure };
            using var reader = await cmd.ExecuteReaderAsync();
            var users = new List<ExpenseUser>();
            while (await reader.ReadAsync())
            {
                users.Add(new ExpenseUser
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                    RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                    ManagerId = reader.IsDBNull(reader.GetOrdinal("ManagerId")) ? null : reader.GetInt32(reader.GetOrdinal("ManagerId")),
                    ManagerName = reader.IsDBNull(reader.GetOrdinal("ManagerName")) ? null : reader.GetString(reader.GetOrdinal("ManagerName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }
            return users;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 185);
            return GetDummyUsers();
        }
    }

    public async Task<List<ExpenseCategory>> GetAllCategoriesAsync()
    {
        try
        {
            _lastError = null;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetAllCategories", conn) { CommandType = CommandType.StoredProcedure };
            using var reader = await cmd.ExecuteReaderAsync();
            var categories = new List<ExpenseCategory>();
            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
            return categories;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 207);
            return GetDummyCategories();
        }
    }

    public async Task<List<ExpenseStatus>> GetAllStatusesAsync()
    {
        try
        {
            _lastError = null;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetAllStatuses", conn) { CommandType = CommandType.StoredProcedure };
            using var reader = await cmd.ExecuteReaderAsync();
            var statuses = new List<ExpenseStatus>();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            }
            return statuses;
        }
        catch (Exception ex)
        {
            SetError(ex, "Services/ExpenseService.cs", 225);
            return GetDummyStatuses();
        }
    }

    private async Task<List<Expense>> ReadExpenses(SqlCommand cmd)
    {
        using var reader = await cmd.ExecuteReaderAsync();
        var expenses = new List<Expense>();
        while (await reader.ReadAsync())
        {
            expenses.Add(new Expense
            {
                ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
                UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                UserName = reader.GetString(reader.GetOrdinal("UserName")),
                Email = reader.GetString(reader.GetOrdinal("Email")),
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
                ReviewedByName = reader.IsDBNull(reader.GetOrdinal("ReviewedByName")) ? null : reader.GetString(reader.GetOrdinal("ReviewedByName")),
                ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            });
        }
        return expenses;
    }

    // ---- Dummy data for when DB is unavailable ----
    public static List<Expense> GetDummyExpenses() => new()
    {
        new Expense { ExpenseId = 1, UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", AmountMinor = 2540, AmountGBP = 25.40m, Currency = "GBP", ExpenseDate = new DateTime(2025, 10, 20), Description = "Taxi from airport to client site (DEMO DATA)", CreatedAt = DateTime.UtcNow.AddDays(-5) },
        new Expense { ExpenseId = 2, UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", CategoryId = 2, CategoryName = "Meals", StatusId = 3, StatusName = "Approved", AmountMinor = 1425, AmountGBP = 14.25m, Currency = "GBP", ExpenseDate = new DateTime(2025, 9, 15), Description = "Client lunch meeting (DEMO DATA)", ReviewedByName = "Bob Manager", ReviewedAt = DateTime.UtcNow.AddDays(-20), CreatedAt = DateTime.UtcNow.AddDays(-21) },
        new Expense { ExpenseId = 3, UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", CategoryId = 3, CategoryName = "Supplies", StatusId = 1, StatusName = "Draft", AmountMinor = 799, AmountGBP = 7.99m, Currency = "GBP", ExpenseDate = new DateTime(2025, 11, 1), Description = "Office stationery (DEMO DATA)", CreatedAt = DateTime.UtcNow.AddDays(-1) },
        new Expense { ExpenseId = 4, UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", CategoryId = 4, CategoryName = "Accommodation", StatusId = 3, StatusName = "Approved", AmountMinor = 12300, AmountGBP = 123.00m, Currency = "GBP", ExpenseDate = new DateTime(2025, 8, 10), Description = "Hotel during client visit (DEMO DATA)", ReviewedByName = "Bob Manager", CreatedAt = DateTime.UtcNow.AddDays(-50) }
    };

    public static List<ExpenseUser> GetDummyUsers() => new()
    {
        new ExpenseUser { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true, CreatedAt = DateTime.UtcNow.AddYears(-1) },
        new ExpenseUser { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true, CreatedAt = DateTime.UtcNow.AddYears(-2) }
    };

    public static List<ExpenseCategory> GetDummyCategories() => new()
    {
        new ExpenseCategory { CategoryId = 1, CategoryName = "Travel", IsActive = true },
        new ExpenseCategory { CategoryId = 2, CategoryName = "Meals", IsActive = true },
        new ExpenseCategory { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
        new ExpenseCategory { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
        new ExpenseCategory { CategoryId = 5, CategoryName = "Other", IsActive = true }
    };

    public static List<ExpenseStatus> GetDummyStatuses() => new()
    {
        new ExpenseStatus { StatusId = 1, StatusName = "Draft" },
        new ExpenseStatus { StatusId = 2, StatusName = "Submitted" },
        new ExpenseStatus { StatusId = 3, StatusName = "Approved" },
        new ExpenseStatus { StatusId = 4, StatusName = "Rejected" }
    };
}
