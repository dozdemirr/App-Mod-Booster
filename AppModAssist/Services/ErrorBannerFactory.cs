using System.Diagnostics;
using AppModAssist.Models;

namespace AppModAssist.Services;

public static class ErrorBannerFactory
{
    public static ApiErrorBanner Create(Exception exception)
    {
        var frame = new StackTrace(exception, true).GetFrames()?.FirstOrDefault(x => x.GetFileLineNumber() > 0);
        var message = $"Database connection failed. Showing fallback data. {exception.GetType().Name}: {exception.Message}. " +
                      "Managed identity fix: ensure the app service user-assigned identity is attached, AZURE_CLIENT_ID is set to the MI client id, " +
                      "the SQL user exists from EXTERNAL PROVIDER, and db_datareader/db_datawriter/EXECUTE permissions are granted.";

        var lower = exception.ToString().ToLowerInvariant();
        return new ApiErrorBanner
        {
            Message = message,
            File = frame?.GetFileName(),
            Line = frame?.GetFileLineNumber() is > 0 ? frame.GetFileLineNumber() : null,
            IsManagedIdentityIssue = lower.Contains("managed identity") || lower.Contains("aad") || lower.Contains("token")
        };
    }
}
