namespace MusiQL.Api;

public sealed record HealthReport(string Service, bool DatabaseConnected)
{
    public string Status => DatabaseConnected ? "healthy" : "degraded";
}
