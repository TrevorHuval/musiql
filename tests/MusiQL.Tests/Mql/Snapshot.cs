using System.Text;
using MusiQL.Core.Mql.Compilation;

namespace MusiQL.Tests.Mql;

internal static class Snapshot
{
    public static string Of(CompiledQuery query)
    {
        var builder = new StringBuilder();
        builder.AppendLine(query.Sql);
        builder.AppendLine("--");
        foreach (var parameter in query.Parameters)
        {
            builder.Append(parameter.Name).Append(" = ").AppendLine(Describe(parameter.Value));
        }

        return builder.ToString().ReplaceLineEndings("\n").TrimEnd('\n');
    }

    private static string Describe(object value) => value switch
    {
        string s => $"text \"{s}\"",
        long l => $"bigint {l}",
        int i => $"int {i}",
        Guid g => $"uuid {g}",
        string[] a => $"text[] [{string.Join(", ", a.Select(x => $"\"{x}\""))}]",
        long[] a => $"bigint[] [{string.Join(", ", a)}]",
        _ => $"{value.GetType().Name} {value}"
    };
}
