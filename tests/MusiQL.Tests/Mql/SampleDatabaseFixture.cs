using System.Net.Sockets;
using System.Runtime.CompilerServices;
using MusiQL.Core.Mql;
using MusiQL.Core.Mql.Compilation;
using MusiQL.Core.Mql.Execution;
using MusiQL.Data;
using MusiQL.Etl.Loading;
using Npgsql;

namespace MusiQL.Tests.Mql;

public sealed class SampleDatabaseFixture
{
    private const string Server = "Host=localhost;Port=5442;Username=musiql;Password=musiql";

    public SampleDatabaseFixture()
    {
        Available = ServerReachable();
        if (!Available)
        {
            return;
        }

        new CatalogLoader(ConnectionString, _ => { }).Load(new DirectoryDumpSource(SampleDirectory()));
    }

    public bool Available { get; }

    public string ConnectionString => $"{Server};Database=musiql_mql_test";

    public MqlEngine Engine { get; } = MqlEngine.CreateDefault();

    public MusiQLDbContext CreateContext() => MusiQLDbContextFactory.Create(ConnectionString);

    public async Task<QueryResult> RunAsync(string mql, Guid? userId = null)
    {
        var compilation = Engine.Compile(mql, new CompileContext { CallerUserId = userId });
        if (!compilation.Success)
        {
            throw new InvalidOperationException(compilation.Errors[0].Message);
        }

        var executor = new QueryExecutor(new ExecutionOptions(ConnectionString));
        return await executor.ExecuteAsync(compilation.Query!);
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
}
