namespace MusiQL.Core.Mql.Compilation;

public sealed record CompileContext
{
    public Guid? CallerUserId { get; init; }

    public int RowCap { get; init; } = MqlLimits.MaxRowCap;

    // Lower-cased genre names with few enough links that resolving their
    // members up front and probing by id beats filtering row by row.
    public IReadOnlySet<string> SelectiveGenres { get; init; } = new HashSet<string>();

    public static CompileContext Default { get; } = new();
}
