using AppModAssist.Models;

namespace AppModAssist.Services;

public interface IExpenseDataService
{
    Task<ApiResponse<DashboardData>> GetDashboardAsync(int? userId, int? categoryId, int? statusId, CancellationToken cancellationToken);
    Task<ApiResponse<ExpenseItem>> CreateExpenseAsync(CreateExpenseRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<bool>> SubmitExpenseAsync(SubmitExpenseRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<bool>> ReviewExpenseAsync(ReviewExpenseRequest request, CancellationToken cancellationToken);
}
