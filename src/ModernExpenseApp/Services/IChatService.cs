namespace ModernExpenseApp.Services;

public interface IChatService
{
    Task<string> AskAsync(string userMessage, CancellationToken cancellationToken);
}
