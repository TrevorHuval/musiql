using MusiQL.Api;

namespace MusiQL.Tests;

public class HealthReportTests
{
    [Fact]
    public void Status_is_healthy_when_database_connected()
    {
        var report = new HealthReport("musiql-api", DatabaseConnected: true);
        Assert.Equal("healthy", report.Status);
    }

    [Fact]
    public void Status_is_degraded_when_database_unreachable()
    {
        var report = new HealthReport("musiql-api", DatabaseConnected: false);
        Assert.Equal("degraded", report.Status);
    }
}
