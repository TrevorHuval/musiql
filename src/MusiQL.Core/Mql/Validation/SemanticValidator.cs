using MusiQL.Core.Mql.Ast;
using MusiQL.Core.Mql.Diagnostics;
using MusiQL.Core.Mql.Schema;

namespace MusiQL.Core.Mql.Validation;

public sealed class SemanticValidator
{
    private readonly SchemaRegistry _registry;
    private readonly List<MqlError> _errors = [];
    private int _predicateCount;

    private SemanticValidator(SchemaRegistry registry) => _registry = registry;

    public static IReadOnlyList<MqlError> Validate(
        MqlQuery query, SchemaRegistry registry, bool hasCallerUserId)
    {
        var validator = new SemanticValidator(registry);
        validator.Run(query, hasCallerUserId);
        return validator._errors;
    }

    private void Run(MqlQuery query, bool hasCallerUserId)
    {
        var entity = _registry.Entity(query.Entity.Name);
        if (entity is null)
        {
            _errors.Add(new MqlError(
                MqlErrorCode.UnknownEntity,
                $"Unknown entity '{query.Entity.Name}'. Valid entities are {ValidEntities()}.",
                query.Entity.Span,
                _registry.EntityNames.OrderBy(n => n).ToList()));
            return;
        }

        if (query.FromLibrary && !hasCallerUserId)
        {
            _errors.Add(new MqlError(
                MqlErrorCode.LibraryUserRequired,
                "'from library' requires an authenticated user.",
                query.Span));
        }

        if (query.Where is not null)
        {
            ValidateExpr(query.Where, entity);
        }

        if (_predicateCount > MqlLimits.MaxPredicates)
        {
            _errors.Add(new MqlError(
                MqlErrorCode.TooManyClauses,
                $"Query has too many conditions (limit is {MqlLimits.MaxPredicates}).",
                query.Span));
        }

        ValidateOrderBy(query, entity);
        ValidateLimit(query);
    }

    private void ValidateExpr(MqlExpr expr, EntitySchema entity)
    {
        switch (expr)
        {
            case AndExpr and:
                ValidateExpr(and.Left, entity);
                ValidateExpr(and.Right, entity);
                break;
            case OrExpr or:
                ValidateExpr(or.Left, entity);
                ValidateExpr(or.Right, entity);
                break;
            case NotExpr not:
                ValidateExpr(not.Operand, entity);
                break;
            case ComparisonExpr comparison:
                ValidatePredicate(comparison.Field, ToOperator(comparison.Op), comparison.Span, entity,
                    field => CheckType(field, comparison.Value));
                break;
            case InExpr inExpr:
                ValidatePredicate(inExpr.Field, MqlOperator.In, inExpr.Span, entity, field =>
                {
                    if (inExpr.Values.Count > MqlLimits.MaxInListItems)
                    {
                        _errors.Add(new MqlError(
                            MqlErrorCode.TooManyClauses,
                            $"'in' list has too many values (limit is {MqlLimits.MaxInListItems}).",
                            inExpr.Span));
                    }

                    foreach (var value in inExpr.Values)
                    {
                        CheckType(field, value);
                    }
                });
                break;
            case BetweenExpr between:
                ValidatePredicate(between.Field, MqlOperator.Between, between.Span, entity, field =>
                {
                    CheckType(field, between.Low);
                    CheckType(field, between.High);
                });
                break;
            case ContainsExpr contains:
                ValidatePredicate(contains.Field, MqlOperator.Contains, contains.Span, entity,
                    field => CheckType(field, contains.Value));
                break;
        }
    }

    private void ValidatePredicate(
        FieldRef fieldRef, MqlOperator op, TextSpan span, EntitySchema entity, Action<FieldSchema> checkValues)
    {
        _predicateCount++;
        var field = entity.Field(fieldRef.Name);
        if (field is null)
        {
            AddUnknownField(fieldRef, entity);
            return;
        }

        if (!field.Allows(op))
        {
            _errors.Add(new MqlError(
                MqlErrorCode.OperatorNotAllowed,
                $"Operator '{MqlOperatorText.Describe(op)}' cannot be used with field '{field.Name}'.",
                span));
            return;
        }

        checkValues(field);
    }

    private void CheckType(FieldSchema field, MqlLiteral literal)
    {
        if (literal.Type != field.ValueType)
        {
            _errors.Add(new MqlError(
                MqlErrorCode.TypeMismatch,
                $"Field '{field.Name}' expects {Describe(field.ValueType)} but got {Describe(literal.Type)}.",
                literal.Span));
        }
    }

    private void ValidateOrderBy(MqlQuery query, EntitySchema entity)
    {
        if (query.OrderBy.Count > MqlLimits.MaxOrderKeys)
        {
            _errors.Add(new MqlError(
                MqlErrorCode.TooManyClauses,
                $"'order by' has too many fields (limit is {MqlLimits.MaxOrderKeys}).",
                query.Span));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in query.OrderBy)
        {
            var field = entity.Field(key.Field.Name);
            if (field is null)
            {
                AddUnknownField(key.Field, entity);
                continue;
            }

            if (!field.Orderable)
            {
                _errors.Add(new MqlError(
                    MqlErrorCode.OperatorNotAllowed,
                    $"Field '{field.Name}' cannot be used in 'order by'.",
                    key.Field.Span));
                continue;
            }

            if (!seen.Add(field.Name))
            {
                _errors.Add(new MqlError(
                    MqlErrorCode.DuplicateOrderField,
                    $"Field '{field.Name}' appears more than once in 'order by'.",
                    key.Field.Span));
            }
        }
    }

    private void ValidateLimit(MqlQuery query)
    {
        if (query.Limit is int limit && limit < 1)
        {
            _errors.Add(new MqlError(
                MqlErrorCode.LimitOutOfRange,
                "Limit must be at least 1.",
                query.LimitSpan));
        }
    }

    private void AddUnknownField(FieldRef fieldRef, EntitySchema entity)
    {
        _errors.Add(new MqlError(
            MqlErrorCode.UnknownField,
            $"Unknown field '{fieldRef.Name}' for {entity.Name}.",
            fieldRef.Span,
            entity.FieldNames.OrderBy(n => n).ToList()));
    }

    private string ValidEntities() =>
        string.Join(", ", _registry.EntityNames.OrderBy(n => n));

    private static MqlOperator ToOperator(ComparisonOp op) => op switch
    {
        ComparisonOp.Equal => MqlOperator.Equal,
        ComparisonOp.NotEqual => MqlOperator.NotEqual,
        ComparisonOp.Less => MqlOperator.Less,
        ComparisonOp.LessEqual => MqlOperator.LessEqual,
        ComparisonOp.Greater => MqlOperator.Greater,
        ComparisonOp.GreaterEqual => MqlOperator.GreaterEqual,
        _ => throw new ArgumentOutOfRangeException(nameof(op))
    };

    private static string Describe(MqlType type) =>
        type == MqlType.Number ? "a number" : "a quoted value";
}
