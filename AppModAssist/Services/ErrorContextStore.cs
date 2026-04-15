using AppModAssist.Models;

namespace AppModAssist.Services;

public sealed class ErrorContextStore
{
    private readonly Lock _lock = new();
    private UiErrorDetails? _latest;

    public void Set(string message)
    {
        lock (_lock)
        {
            _latest = new UiErrorDetails(message, DateTimeOffset.UtcNow);
        }
    }

    public UiErrorDetails? Get()
    {
        lock (_lock)
        {
            return _latest;
        }
    }
}
