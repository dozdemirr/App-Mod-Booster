using AppModAssist.Models;

namespace AppModAssist.Services;

public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseItem>> GetExpensesAsync(ExpenseFilter filter, CancellationToken cancellationToken);
    Task<int> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken);
    Task UpdateExpenseStatusAsync(UpdateExpenseStatusRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<LookupItem>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LookupItem>> GetStatusesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LookupItem>> GetUsersAsync(CancellationToken cancellationToken);
}
