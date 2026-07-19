using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MusiQL.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMusiQLData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<MusiQLDbContext>(options => options.UseNpgsql(connectionString));
        return services;
    }
}
