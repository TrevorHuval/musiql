using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MusiQL.Tests.Api;

public sealed class MusiQLApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:MusiQL", connectionString);
        builder.UseSetting("Jwt:Issuer", "musiql-test");
        builder.UseSetting("Jwt:Audience", "musiql-test");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-that-is-definitely-long-enough-0123456789abcdef");
        builder.UseSetting("Jwt:AccessTokenMinutes", "30");
    }
}
