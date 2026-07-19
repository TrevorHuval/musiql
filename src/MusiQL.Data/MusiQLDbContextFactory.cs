using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MusiQL.Data;

public class MusiQLDbContextFactory : IDesignTimeDbContextFactory<MusiQLDbContext>
{
    private const string DevConnection =
        "Host=localhost;Port=5442;Database=musiql;Username=musiql;Password=musiql";

    public MusiQLDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("MUSIQL_CONNECTION") ?? DevConnection;
        return Create(connection);
    }

    public static MusiQLDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<MusiQLDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new MusiQLDbContext(options);
    }
}
