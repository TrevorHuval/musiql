using MusiQL.Core.Mql.Schema;

namespace MusiQL.Core.Mql.Execution;

public sealed record QueryResult(
    IReadOnlyList<ResultColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows);
