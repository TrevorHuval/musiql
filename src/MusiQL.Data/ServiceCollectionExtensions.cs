using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusiQL.Data.App;

namespace MusiQL.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMusiQLData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<MusiQLDbContext>(options => options.UseNpgsql(connectionString));
        return services;
    }

    public static IServiceCollection AddMusiQLApp(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        return services;
    }
}
