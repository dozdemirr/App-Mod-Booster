using AppModAssist.Models;

namespace AppModAssist.Services;

public interface IChatService
{
    Task<ChatResponse> AskAsync(string message, CancellationToken cancellationToken);
}
