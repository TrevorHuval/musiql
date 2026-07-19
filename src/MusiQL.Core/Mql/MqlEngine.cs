using MusiQL.Core.Mql.Ast;
using MusiQL.Core.Mql.Compilation;
using MusiQL.Core.Mql.Diagnostics;
using MusiQL.Core.Mql.Parsing;
using MusiQL.Core.Mql.Schema;
using MusiQL.Core.Mql.Validation;

namespace MusiQL.Core.Mql;

public sealed record MqlCompilation(CompiledQuery? Query, IReadOnlyList<MqlError> Errors)
{
    public bool Success => Query is not null;
}

public sealed class MqlEngine(SchemaRegistry registry)
{
    public SchemaRegistry Registry => registry;

    public static MqlEngine CreateDefault() => new(CatalogRegistry.Create());

    public MqlCompilation Compile(string mql, CompileContext context)
    {
        MqlQuery query;
        try
        {
            query = Parser.Parse(mql);
        }
        catch (MqlException ex)
        {
            return new MqlCompilation(null, [ex.Error]);
        }

        var errors = SemanticValidator.Validate(query, registry, context.CallerUserId.HasValue);
        if (errors.Count > 0)
        {
            return new MqlCompilation(null, errors);
        }

        return new MqlCompilation(SqlCompiler.Compile(query, registry, context), []);
    }
}
