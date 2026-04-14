namespace AppModAssist.Models;

public class ApiResponse<T>
{
    public required T Data { get; init; }
    public ApiErrorBanner? ErrorBanner { get; init; }
    public bool UsedFallback { get; init; }
}
