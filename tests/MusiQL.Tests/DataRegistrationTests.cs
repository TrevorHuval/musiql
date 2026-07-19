using Microsoft.Extensions.DependencyInjection;
using MusiQL.Data;

namespace MusiQL.Tests;

public class DataRegistrationTests
{
    [Fact]
    public void AddMusiQLData_registers_the_db_context()
    {
        var services = new ServiceCollection();
        services.AddMusiQLData("Host=localhost;Database=musiql;Username=musiql;Password=musiql");

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<MusiQLDbContext>());
    }
}
