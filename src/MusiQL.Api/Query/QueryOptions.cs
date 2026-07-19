namespace MusiQL.Api.Query;

public sealed class QueryOptions
{
    public string ConnectionString { get; set; } = "";
    public TimeSpan StatementTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
