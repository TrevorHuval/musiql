namespace MusiQL.Etl.Tsv;

public static class Mbdump
{
    public static string[] SplitFields(string line) => line.Split('\t');

    public static string? Field(string[] fields, int index)
    {
        if (index >= fields.Length)
        {
            return null;
        }

        var raw = fields[index];
        return TsvValue.IsNull(raw) ? null : TsvValue.Unescape(raw);
    }
}
