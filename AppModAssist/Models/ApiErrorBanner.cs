namespace AppModAssist.Models;

public class ApiErrorBanner
{
    public required string Message { get; init; }
    public string? File { get; init; }
    public int? Line { get; init; }
    public bool IsManagedIdentityIssue { get; init; }
}
