namespace MusiQL.Etl.Loading;

public sealed class DirectoryDumpSource(string mbdumpDirectory) : IDumpSource
{
    public IEnumerable<DumpTable> Read(ISet<string> wanted)
    {
        foreach (var table in wanted)
        {
            var path = Path.Combine(mbdumpDirectory, table);
            if (!File.Exists(path))
            {
                continue;
            }

            using var reader = new StreamReader(path);
            yield return new DumpTable(table, reader);
        }
    }
}
