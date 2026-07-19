namespace MusiQL.Api.Contracts;

public sealed record PreviewRequest(string Mql, int? Page, int? PageSize);

public sealed record QueryColumn(string Name, string Type);

public sealed record QueryPageResponse(
    string Entity,
    IReadOnlyList<QueryColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int Page,
    int PageSize,
    int Total,
    bool HasMore,
    string? Hint);

public sealed record MqlErrorDto(
    string Code,
    string Message,
    int Start,
    int Length,
    IReadOnlyList<string>? Expected);
