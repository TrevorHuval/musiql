namespace MusiQL.Core.Mql.Diagnostics;

public sealed record MqlError(
    MqlErrorCode Code,
    string Message,
    TextSpan Span,
    IReadOnlyList<string>? Expected = null);
