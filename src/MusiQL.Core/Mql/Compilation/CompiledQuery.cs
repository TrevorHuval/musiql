using MusiQL.Core.Mql.Schema;

namespace MusiQL.Core.Mql.Compilation;

public sealed record CompiledQuery(
    string Sql,
    IReadOnlyList<MqlParameter> Parameters,
    string Entity,
    IReadOnlyList<ResultColumn> ResultColumns,
    int RowLimit);
