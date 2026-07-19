namespace MusiQL.Etl.Loading;

public record DumpTable(string Table, TextReader Reader);

public interface IDumpSource
{
    IEnumerable<DumpTable> Read(ISet<string> wanted);
}
