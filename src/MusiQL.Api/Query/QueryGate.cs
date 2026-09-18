using Microsoft.Extensions.Options;

namespace MusiQL.Api.Query;

public sealed class QueryBusyException(TimeSpan retryAfter) : Exception("The query engine is at capacity.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}

// Bounds how many MQL executions hold a database connection at once, regardless
// of which route asked. Callers wait briefly for a slot and are turned away with
// 503 + Retry-After rather than queued indefinitely.
public sealed class QueryGate(IOptions<QueryOptions> options) : IDisposable
{
    private readonly SemaphoreSlim _slots = new(
        Math.Max(1, options.Value.MaxConcurrent), Math.Max(1, options.Value.MaxConcurrent));

    private TimeSpan AdmissionTimeout => options.Value.AdmissionTimeout;

    public async Task<IDisposable> EnterAsync(CancellationToken ct)
    {
        if (!await _slots.WaitAsync(AdmissionTimeout, ct))
        {
            throw new QueryBusyException(AdmissionTimeout);
        }

        return new Slot(_slots);
    }

    public void Dispose() => _slots.Dispose();

    private sealed class Slot(SemaphoreSlim slots) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                slots.Release();
            }
        }
    }
}
