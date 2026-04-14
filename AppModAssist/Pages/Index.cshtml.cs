using AppModAssist.Models;
using AppModAssist.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AppModAssist.Pages;

public class IndexModel : PageModel
{
    private readonly ExpenseApiClient _apiClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ExpenseApiClient apiClient, ILogger<IndexModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)] public int? FilterUserId { get; set; }
    [BindProperty(SupportsGet = true)] public int? FilterCategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public int? FilterStatusId { get; set; }
    [BindProperty] public CreateExpenseRequest NewExpense { get; set; } = new(1, 1, 10m, DateOnly.FromDateTime(DateTime.UtcNow.Date), "New expense", null);
    [BindProperty] public int SubmitExpenseId { get; set; }
    [BindProperty] public int ReviewExpenseId { get; set; }
    [BindProperty] public int ReviewedByUserId { get; set; } = 2;
    [BindProperty] public bool Approve { get; set; } = true;
    [BindProperty] public string? ReviewNotes { get; set; }

    public DashboardData Data { get; private set; } = new([], [], [], []);
    public ApiErrorBanner? ErrorBanner { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var created = await _apiClient.CreateExpenseAsync(NewExpense, cancellationToken);
        ErrorBanner = created?.ErrorBanner;
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSubmitAsync(CancellationToken cancellationToken)
    {
        var submitted = await _apiClient.SubmitExpenseAsync(SubmitExpenseId, cancellationToken);
        ErrorBanner = submitted?.ErrorBanner;
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostReviewAsync(CancellationToken cancellationToken)
    {
        var reviewed = await _apiClient.ReviewExpenseAsync(new ReviewExpenseRequest(ReviewExpenseId, ReviewedByUserId, Approve, ReviewNotes), cancellationToken);
        ErrorBanner = reviewed?.ErrorBanner;
        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var dashboard = await _apiClient.GetDashboardAsync(FilterUserId, FilterCategoryId, FilterStatusId, cancellationToken);
        Data = dashboard?.Data ?? new DashboardData([], [], [], []);
        ErrorBanner ??= dashboard?.ErrorBanner;
    }
}
