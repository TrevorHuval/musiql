using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MusiQL.Tests.Api;

// Every request from the test server shares one client address, so the
// per-address limits are raised far above what a test run produces unless a
// test asks for specific values.
public sealed class MusiQLApiFactory(
    string connectionString, IReadOnlyDictionary<string, string>? settings = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:MusiQL", connectionString);
        builder.UseSetting("Jwt:Issuer", "musiql-test");
        builder.UseSetting("Jwt:Audience", "musiql-test");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-that-is-definitely-long-enough-0123456789abcdef");
        builder.UseSetting("Jwt:AccessTokenMinutes", "30");
        builder.UseSetting("Limits:RequestsPerTenSeconds", "100000");
        builder.UseSetting("Limits:AuthPerMinute", "100000");
        builder.UseSetting("Limits:RegistrationsPerHour", "100000");
        builder.UseSetting("Limits:WritesPerMinute", "100000");

        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }
    }
}
