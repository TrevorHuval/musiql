using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MusiQL.Data.App;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DevConnection =
        "Host=localhost;Port=5442;Database=musiql;Username=musiql;Password=musiql";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("MUSIQL_CONNECTION") ?? DevConnection;
        return Create(connection);
    }

    public static AppDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AppDbContext(options);
    }
}
