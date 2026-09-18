namespace MusiQL.Api;

public sealed record HealthReport(string Service, bool DatabaseConnected, bool CatalogLoaded = false)
{
    public string Status => DatabaseConnected ? "healthy" : "degraded";
}
