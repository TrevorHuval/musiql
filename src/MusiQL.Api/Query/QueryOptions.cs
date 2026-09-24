namespace MusiQL.Api.Query;

public sealed class QueryOptions
{
    public string ConnectionString { get; set; } = "";
    public TimeSpan StatementTimeout { get; set; } = TimeSpan.FromSeconds(5);

    // Cached playlist results are served until this age elapses or the definition
    // changes. Zero disables snapshotting and executes every read.
    public TimeSpan SnapshotTtl { get; set; } = TimeSpan.FromMinutes(10);

    // Executions allowed to hold a catalog connection at the same time, across
    // all users, and how long a request waits for a slot before giving up.
    public int MaxConcurrent { get; set; } = 4;
    public TimeSpan AdmissionTimeout { get; set; } = TimeSpan.FromSeconds(2);

    // Genres with fewer total genre links than this compile to an id-probe
    // filter; see SqlCompiler.GenreMembership.
    public int SelectiveGenreLinks { get; set; } = 20000;

    // Production refuses to run MQL over the owner connection unless this is set.
    public bool AllowOwnerConnection { get; set; }
}
