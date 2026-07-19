namespace MusiQL.Core.Mql.Compilation;

public sealed record CompileContext
{
    public Guid? CallerUserId { get; init; }

    public int RowCap { get; init; } = MqlLimits.MaxRowCap;

    public static CompileContext Default { get; } = new();
}
