using System.Text;
using MusiQL.Core.Mql.Execution;

namespace MusiQL.Api.Query;

public static class M3uExport
{
    public static string Build(QueryResult result)
    {
        var id = ColumnIndex(result, "id");
        var title = ColumnIndex(result, "title");
        var artist = ColumnIndex(result, "artist");
        var length = ColumnIndex(result, "length_ms");

        var m3u = new StringBuilder("#EXTM3U\n");
        foreach (var row in result.Rows)
        {
            var seconds = row[length] is int ms ? ms / 1000 : -1;
            var artistName = Clean(row[artist] as string);
            var trackTitle = Clean(row[title] as string);

            m3u.Append("#EXTINF:").Append(seconds).Append(',')
                .Append(artistName).Append(" - ").Append(trackTitle).Append('\n');
            m3u.Append(row[id] is Guid mbid
                ? $"https://musicbrainz.org/recording/{mbid}"
                : trackTitle).Append('\n');
        }

        return m3u.ToString();
    }

    private static int ColumnIndex(QueryResult result, string name)
    {
        for (var i = 0; i < result.Columns.Count; i++)
        {
            if (result.Columns[i].Name == name)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"Result has no '{name}' column.");
    }

    private static string Clean(string? value) =>
        value is null ? "" : value.ReplaceLineEndings(" ");
}
