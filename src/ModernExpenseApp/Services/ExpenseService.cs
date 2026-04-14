using ModernExpenseApp.Models;

namespace ModernExpenseApp.Services;

public sealed class ExpenseService(IExpenseRepository repository, ErrorBannerService errorBannerService, ILogger<ExpenseService> logger) : IExpenseService
{
    public async Task<IReadOnlyList<ExpenseDto>> GetExpensesAsync(int? statusId, int? userId, CancellationToken cancellationToken)
    {
        try
        {
            var data = await repository.GetExpensesAsync(statusId, userId, cancellationToken);
            errorBannerService.Clear();
            return data;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load expenses, returning dummy data.");
            errorBannerService.Capture(ex);
            return DummyData.Expenses;
        }
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await repository.CreateExpenseAsync(request, cancellationToken);
            errorBannerService.Clear();
            return id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create expense.");
            errorBannerService.Capture(ex);
            return 0;
        }
    }

    public async Task ReviewExpenseAsync(int expenseId, int managerUserId, bool approve, CancellationToken cancellationToken)
    {
        try
        {
            await repository.ReviewExpenseAsync(expenseId, managerUserId, approve, cancellationToken);
            errorBannerService.Clear();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to review expense.");
            errorBannerService.Capture(ex);
        }
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var data = await repository.GetUsersAsync(cancellationToken);
            errorBannerService.Clear();
            return data;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load users.");
            errorBannerService.Capture(ex);
            return DummyData.Users;
        }
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var data = await repository.GetCategoriesAsync(cancellationToken);
            errorBannerService.Clear();
            return data;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load categories.");
            errorBannerService.Capture(ex);
            return DummyData.Categories;
        }
    }

    public async Task<IReadOnlyList<StatusDto>> GetStatusesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var data = await repository.GetStatusesAsync(cancellationToken);
            errorBannerService.Clear();
            return data;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load statuses.");
            errorBannerService.Capture(ex);
            return DummyData.Statuses;
        }
    }
}

internal static class DummyData
{
    public static readonly IReadOnlyList<UserDto> Users =
    [
        new() { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleName = "Employee" },
        new() { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleName = "Manager" }
    ];

    public static readonly IReadOnlyList<CategoryDto> Categories =
    [
        new() { CategoryId = 1, CategoryName = "Travel" },
        new() { CategoryId = 2, CategoryName = "Meals" },
        new() { CategoryId = 3, CategoryName = "Supplies" },
        new() { CategoryId = 4, CategoryName = "Accommodation" },
        new() { CategoryId = 5, CategoryName = "Other" }
    ];

    public static readonly IReadOnlyList<StatusDto> Statuses =
    [
        new() { StatusId = 1, StatusName = "Draft" },
        new() { StatusId = 2, StatusName = "Submitted" },
        new() { StatusId = 3, StatusName = "Approved" },
        new() { StatusId = 4, StatusName = "Rejected" }
    ];

    public static readonly IReadOnlyList<ExpenseDto> Expenses =
    [
        new()
        {
            ExpenseId = 1001,
            UserId = 1,
            UserName = "Alice Example",
            CategoryId = 1,
            CategoryName = "Travel",
            StatusId = 2,
            StatusName = "Submitted",
            AmountMinor = 2540,
            Currency = "GBP",
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            Description = "Dummy fallback taxi expense",
            SubmittedAt = DateTime.UtcNow.AddDays(-2)
        }
    ];
}
