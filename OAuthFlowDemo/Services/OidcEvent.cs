namespace OAuthFlowDemo.Services;

public enum EventCategory
{
    Protocol,       // OAuth2/OIDC authorization flow steps
    ApiAuth,        // JWT Bearer token validation for API calls
    Infrastructure, // ASP.NET Core middleware (cookie plumbing, redirects)
    Simulated,      // Testing mode fabricated events
}

public sealed record OidcEvent
{
    private static long _nextSequence;

    public Guid Id { get; init; } = Guid.NewGuid();

    public long Sequence { get; init; } = Interlocked.Increment(ref _nextSequence);

    public required string EventType { get; init; }

    public string? Description { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public required string FlowPhase { get; init; }

    public EventCategory Category { get; init; } = EventCategory.Protocol;

    public string? RequestPath { get; init; }

    public Dictionary<string, string> Details { get; init; } = [];
}
