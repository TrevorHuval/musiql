namespace MusiQL.Core.Mql.Execution;

public sealed record ExecutionOptions(string ConnectionString)
{
    public TimeSpan StatementTimeout { get; init; } = TimeSpan.FromSeconds(5);
}
