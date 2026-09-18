using System.Collections.Concurrent;

namespace MusiQL.Api;

public sealed class OperationInProgressException(string what)
    : Exception($"{what} is already running.")
{
    public string What { get; } = what;
}

// Non-blocking mutual exclusion for long-running work such as a Spotify export
// or library sync. A second caller for the same key is refused immediately.
public sealed class OperationLocks
{
    private readonly ConcurrentDictionary<string, byte> _held = new();

    public IDisposable Acquire(string key, string what)
    {
        if (!_held.TryAdd(key, 0))
        {
            throw new OperationInProgressException(what);
        }

        return new Release(this, key);
    }

    private sealed class Release(OperationLocks owner, string key) : IDisposable
    {
        public void Dispose() => owner._held.TryRemove(key, out _);
    }
}
