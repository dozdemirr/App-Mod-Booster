using ModernExpenseApp.Models;

namespace ModernExpenseApp.Services;

public interface IExpenseRepository
{
    Task<IReadOnlyList<ExpenseDto>> GetExpensesAsync(int? statusId, int? userId, CancellationToken cancellationToken);
    Task<int> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken);
    Task ReviewExpenseAsync(int expenseId, int managerUserId, bool approve, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<StatusDto>> GetStatusesAsync(CancellationToken cancellationToken);
}
