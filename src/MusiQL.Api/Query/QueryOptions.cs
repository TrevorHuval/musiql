namespace MusiQL.Api.Query;

public sealed class QueryOptions
{
    public string ConnectionString { get; set; } = "";
    public TimeSpan StatementTimeout { get; set; } = TimeSpan.FromSeconds(5);

    // Cached playlist results are served until this age elapses or the definition
    // changes. Zero disables snapshotting and executes every read.
    public TimeSpan SnapshotTtl { get; set; } = TimeSpan.FromMinutes(10);
}
