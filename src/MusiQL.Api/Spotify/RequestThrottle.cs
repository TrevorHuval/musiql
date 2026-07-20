namespace MusiQL.Api.Spotify;

public sealed class RequestThrottle(TimeSpan minInterval)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _last = DateTime.MinValue;

    public async Task WaitAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var wait = minInterval - (DateTime.UtcNow - _last);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, ct);
            }

            _last = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }
}
