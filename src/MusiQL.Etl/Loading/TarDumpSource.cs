using System.Text;
using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.Tar;

namespace MusiQL.Etl.Loading;

public sealed class TarDumpSource(IReadOnlyList<string> tarballs) : IDumpSource
{
    public IEnumerable<DumpTable> Read(ISet<string> wanted)
    {
        foreach (var tarball in tarballs)
        {
            using var file = File.OpenRead(tarball);
            using var bzip = new BZip2InputStream(file);
            using var tar = new TarInputStream(bzip, Encoding.UTF8);

            while (tar.GetNextEntry() is { } entry)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }

                var table = TableName(entry.Name);
                if (table is null || !wanted.Contains(table))
                {
                    continue;
                }

                var reader = new StreamReader(tar, Encoding.UTF8, false, 1 << 20, leaveOpen: true);
                yield return new DumpTable(table, reader);
            }
        }
    }

    private static string? TableName(string entryName)
    {
        var path = entryName.Replace('\\', '/');
        var slash = path.LastIndexOf('/');
        if (slash < 0)
        {
            return null;
        }

        return path[..slash].EndsWith("mbdump", StringComparison.Ordinal)
            ? path[(slash + 1)..]
            : null;
    }
}
