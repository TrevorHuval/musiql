using System.Text;

namespace MusiQL.Etl.Tsv;

public static class TsvValue
{
    public static bool IsNull(string field) =>
        field.Length == 2 && field[0] == '\\' && field[1] == 'N';

    public static string Unescape(string field)
    {
        if (field.IndexOf('\\') < 0)
        {
            return field;
        }

        var sb = new StringBuilder(field.Length);
        for (var i = 0; i < field.Length; i++)
        {
            var c = field[i];
            if (c == '\\' && i + 1 < field.Length)
            {
                var next = field[++i];
                sb.Append(next switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    'b' => '\b',
                    'f' => '\f',
                    'v' => '\v',
                    _ => next
                });
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
