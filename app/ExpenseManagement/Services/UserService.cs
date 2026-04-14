using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public class UserService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserService> _logger;

    public UserService(IConfiguration configuration, ILogger<UserService> logger)
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

    public (List<User> users, string? error) GetAllUsers([CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_GetAllUsers", connection) { CommandType = CommandType.StoredProcedure };
            return (ReadUsers(command), null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetAllUsers));
            return (GetDummyUsers(), FormatError(ex, filePath, lineNumber));
        }
    }

    public (User? user, string? error) GetUserById(int userId, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_GetUserById", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@UserId", userId);
            var users = ReadUsers(command);
            return (users.FirstOrDefault(), null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetUserById));
            return (GetDummyUsers().First(), FormatError(ex, filePath, lineNumber));
        }
    }

    public (bool success, string? error) CreateUser(CreateUserRequest request, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_CreateUser", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@UserName", request.UserName);
            command.Parameters.AddWithValue("@Email", request.Email);
            command.Parameters.AddWithValue("@RoleId", request.RoleId);
            command.Parameters.AddWithValue("@ManagerId", (object?)request.ManagerId ?? DBNull.Value);
            command.ExecuteNonQuery();
            return (true, null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(CreateUser));
            return (false, FormatError(ex, filePath, lineNumber));
        }
    }

    public (List<ExpenseCategory> categories, string? error) GetAllCategories([CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var command = new SqlCommand("usp_GetAllCategories", connection) { CommandType = CommandType.StoredProcedure };
            var categories = new List<ExpenseCategory>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = Convert.ToInt32(reader["CategoryId"]),
                    CategoryName = reader["CategoryName"]?.ToString() ?? "",
                    IsActive = Convert.ToBoolean(reader["IsActive"])
                });
            }
            return (categories, null);
        }
        catch (Exception ex)
        {
            LogError(ex, nameof(GetAllCategories));
            return (GetDummyCategories(), FormatError(ex, filePath, lineNumber));
        }
    }

    private List<User> ReadUsers(SqlCommand command)
    {
        var users = new List<User>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new User
            {
                UserId = Convert.ToInt32(reader["UserId"]),
                UserName = reader["UserName"]?.ToString() ?? "",
                Email = reader["Email"]?.ToString() ?? "",
                RoleName = reader["RoleName"]?.ToString() ?? "",
                ManagerName = reader["ManagerName"] == DBNull.Value ? null : reader["ManagerName"]?.ToString(),
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
            });
        }
        return users;
    }

    private static List<User> GetDummyUsers() => new()
    {
        new User { UserId = 1, UserName = "Jane Smith", Email = "jane.smith@company.com", RoleName = "Employee", IsActive = true, CreatedAt = DateTime.Today.AddMonths(-6) },
        new User { UserId = 2, UserName = "John Doe", Email = "john.doe@company.com", RoleName = "Employee", IsActive = true, CreatedAt = DateTime.Today.AddMonths(-4) },
        new User { UserId = 3, UserName = "Alice Brown", Email = "alice.brown@company.com", RoleName = "Manager", IsActive = true, CreatedAt = DateTime.Today.AddMonths(-12) },
        new User { UserId = 4, UserName = "Bob Wilson", Email = "bob.wilson@company.com", RoleName = "Employee", ManagerName = "Alice Brown", IsActive = true, CreatedAt = DateTime.Today.AddMonths(-2) },
    };

    private static List<ExpenseCategory> GetDummyCategories() => new()
    {
        new ExpenseCategory { CategoryId = 1, CategoryName = "Travel", IsActive = true },
        new ExpenseCategory { CategoryId = 2, CategoryName = "Meals", IsActive = true },
        new ExpenseCategory { CategoryId = 3, CategoryName = "Equipment", IsActive = true },
        new ExpenseCategory { CategoryId = 4, CategoryName = "Software", IsActive = true },
        new ExpenseCategory { CategoryId = 5, CategoryName = "Training", IsActive = true },
    };
}
