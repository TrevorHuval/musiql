namespace MusiQL.Etl;

public sealed class CliOptions
{
    private readonly Dictionary<string, string?> flags = new(StringComparer.Ordinal);

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options.flags[key] = args[++i];
            }
            else
            {
                options.flags[key] = null;
            }
        }

        return options;
    }

    public bool Has(string key) => flags.ContainsKey(key);

    public string? Value(string key) => flags.GetValueOrDefault(key);
}
