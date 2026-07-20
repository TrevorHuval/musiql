using System.Text;

namespace MusiQL.Api.Spotify;

public static class TextSimilarity
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var stripped = StripParentheticals(value);

        var builder = new StringBuilder(stripped.Length);
        var lastSpace = false;
        foreach (var raw in stripped)
        {
            var lowered = char.ToLowerInvariant(raw);
            var folded = Fold(lowered);
            foreach (var ch in folded)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                    lastSpace = false;
                }
                else if (!lastSpace)
                {
                    builder.Append(' ');
                    lastSpace = true;
                }
            }
        }

        return builder.ToString().Trim();
    }

    public static double Ratio(string left, string right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        if (a.Length == 0 && b.Length == 0)
        {
            return 1;
        }

        if (a.Length == 0 || b.Length == 0)
        {
            return 0;
        }

        if (a == b)
        {
            return 1;
        }

        var distance = Levenshtein(a, b);
        return 1.0 - (double)distance / Math.Max(a.Length, b.Length);
    }

    private static string StripParentheticals(string value)
    {
        var builder = new StringBuilder(value.Length);
        var depth = 0;
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '(' or '[':
                    depth++;
                    break;
                case ')' or ']':
                    if (depth > 0)
                    {
                        depth--;
                    }

                    break;
                default:
                    if (depth == 0)
                    {
                        builder.Append(ch);
                    }

                    break;
            }
        }

        var text = builder.ToString();
        var feat = text.IndexOf("feat", StringComparison.OrdinalIgnoreCase);
        if (feat > 0)
        {
            text = text[..feat];
        }

        return text;
    }

    private static string Fold(char ch) => ch switch
    {
        'à' or 'á' or 'â' or 'ã' or 'ä' or 'å' or 'ā' or 'ă' or 'ą' or 'ǎ' => "a",
        'è' or 'é' or 'ê' or 'ë' or 'ē' or 'ĕ' or 'ė' or 'ę' or 'ě' => "e",
        'ì' or 'í' or 'î' or 'ï' or 'ĩ' or 'ī' or 'ĭ' or 'į' or 'ı' => "i",
        'ò' or 'ó' or 'ô' or 'õ' or 'ö' or 'ø' or 'ō' or 'ŏ' or 'ő' => "o",
        'ù' or 'ú' or 'û' or 'ü' or 'ũ' or 'ū' or 'ŭ' or 'ů' or 'ű' or 'ų' => "u",
        'ñ' or 'ń' or 'ņ' or 'ň' => "n",
        'ç' or 'ć' or 'ĉ' or 'ċ' or 'č' => "c",
        'ś' or 'ŝ' or 'ş' or 'š' => "s",
        'ź' or 'ż' or 'ž' => "z",
        'ý' or 'ÿ' => "y",
        'ĝ' or 'ğ' or 'ġ' or 'ģ' => "g",
        'ŕ' or 'ř' => "r",
        'ł' or 'ĺ' or 'ļ' or 'ľ' => "l",
        'ţ' or 'ť' => "t",
        'ď' or 'đ' or 'ð' => "d",
        'ß' => "ss",
        'æ' => "ae",
        'œ' => "oe",
        'þ' => "th",
        _ => ch.ToString()
    };

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
