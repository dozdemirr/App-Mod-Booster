using System.Text.RegularExpressions;

namespace ModernExpenseApp.Services;

public sealed class ErrorBannerService
{
    private string? _currentError;

    public string? CurrentError => _currentError;

    public void Clear() => _currentError = null;

    public void Capture(Exception ex)
    {
        var stackLine = ex.StackTrace?.Split('\n').FirstOrDefault(static line => line.Contains(".cs:line", StringComparison.OrdinalIgnoreCase));
        var pathMatch = stackLine is null ? null : Regex.Match(stackLine, @"in (.*\.cs):line (\d+)");
        var fileText = pathMatch is { Success: true } ? pathMatch.Groups[1].Value : "Unknown source file";
        var lineText = pathMatch is { Success: true } ? pathMatch.Groups[2].Value : "unknown line";

        _currentError =
            $"Database fallback mode is active. Details: {ex.GetType().Name}: {ex.Message}. " +
            $"Source: {fileText}, line {lineText}. " +
            "Managed identity fix: ensure the SQL server has an Entra admin configured, " +
            "create database user from external provider for the app managed identity, grant db_datareader/db_datawriter/execute, " +
            "and verify AZURE_CLIENT_ID matches the user-assigned managed identity client ID.";
    }
}
