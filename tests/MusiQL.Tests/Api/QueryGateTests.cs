using Microsoft.Extensions.Options;
using MusiQL.Api.Query;

namespace MusiQL.Tests.Api;

public class QueryGateTests
{
    [Fact]
    public async Task Overflow_is_refused_after_the_admission_timeout()
    {
        using var gate = new QueryGate(Options.Create(new QueryOptions
        {
            MaxConcurrent = 2,
            AdmissionTimeout = TimeSpan.FromMilliseconds(20)
        }));

        using var first = await gate.EnterAsync(default);
        using var second = await gate.EnterAsync(default);

        var refused = await Assert.ThrowsAsync<QueryBusyException>(() => gate.EnterAsync(default));
        Assert.Equal(TimeSpan.FromMilliseconds(20), refused.RetryAfter);
    }

    [Fact]
    public async Task Releasing_a_slot_admits_the_next_caller()
    {
        using var gate = new QueryGate(Options.Create(new QueryOptions
        {
            MaxConcurrent = 1,
            AdmissionTimeout = TimeSpan.FromSeconds(2)
        }));

        var held = await gate.EnterAsync(default);
        var waiting = gate.EnterAsync(default);
        Assert.False(waiting.IsCompleted);

        held.Dispose();
        using var admitted = await waiting;
        Assert.NotNull(admitted);
    }
}
