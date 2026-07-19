using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using MusiQL.Api.Contracts;
using MusiQL.Data;
using MusiQL.Etl.Loading;
using Npgsql;

namespace MusiQL.Tests.Api;

public sealed class ApiFixture : IDisposable
{
    private const string Server = "Host=localhost;Port=5442;Username=musiql;Password=musiql";
    private readonly MusiQLApiFactory? _factory;

    public ApiFixture()
    {
        Available = ServerReachable();
        if (!Available)
        {
            return;
        }

        new CatalogLoader(ConnectionString, _ => { }).Load(new DirectoryDumpSource(SampleDirectory()));

        using (var app = MusiQL.Data.App.AppDbContextFactory.Create(ConnectionString))
        {
            app.Database.Migrate();
        }

        ResetAppData();
        _factory = new MusiQLApiFactory(ConnectionString);
    }

    public bool Available { get; }

    public string ConnectionString => $"{Server};Database=musiql_api_test";

    public HttpClient CreateClient() => _factory!.CreateClient();

    public MusiQLDbContext CreateCatalogContext() => MusiQLDbContextFactory.Create(ConnectionString);

    public async Task<HttpClient> RegisterClientAsync(string? email = null, string password = "Password123!")
    {
        var client = CreateClient();
        var auth = await RegisterAsync(client, email ?? UniqueEmail(), password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    public async Task<AuthResponse> RegisterAsync(HttpClient client, string email, string password = "Password123!")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private void ResetAppData()
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();
        using var command = new NpgsqlCommand(
            "TRUNCATE app.users CASCADE; TRUNCATE app.user_library;", connection);
        command.ExecuteNonQuery();
    }

    private static bool ServerReachable()
    {
        try
        {
            using var connection = new NpgsqlConnection($"{Server};Database=postgres;Timeout=3");
            connection.Open();
            return true;
        }
        catch (NpgsqlException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static string SampleDirectory([CallerFilePath] string path = "")
    {
        var testDir = Path.GetDirectoryName(path)!;
        return Path.GetFullPath(
            Path.Combine(testDir, "..", "..", "..", "src", "MusiQL.Etl", "sample", "mbdump"));
    }

    public void Dispose() => _factory?.Dispose();
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;
