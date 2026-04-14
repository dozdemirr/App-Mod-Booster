namespace AppModAssist.Models;

public record ChatRequest(string Message);

public record ChatResponse(string Reply, bool UsedFallback, string? Guidance);
