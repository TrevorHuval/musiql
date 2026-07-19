namespace MusiQL.Core.Mql.Diagnostics;

public sealed class MqlException(MqlError error) : Exception(error.Message)
{
    public MqlError Error { get; } = error;
}
