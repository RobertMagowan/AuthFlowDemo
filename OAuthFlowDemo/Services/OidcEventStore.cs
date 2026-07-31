using System.Collections.Concurrent;

namespace OAuthFlowDemo.Services;

public sealed class OidcEventStore
{
    private const int MaxEvents = 500;
    private readonly ConcurrentQueue<OidcEvent> _events = new();

    public void Add(OidcEvent evt)
    {
        _events.Enqueue(evt);
        while (_events.Count > MaxEvents && _events.TryDequeue(out _)) { }
    }

    public IReadOnlyList<OidcEvent> GetAll()
    {
        return [.. _events]; // FIFO order, oldest first
    }

    public OidcEvent? GetLatest()
    {
        return _events.OrderByDescending(e => e.Sequence).FirstOrDefault();
    }

    public void Clear()
    {
        while (_events.TryDequeue(out _)) { }
    }

    public int Count => _events.Count;
}
