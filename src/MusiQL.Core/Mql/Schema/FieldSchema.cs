namespace MusiQL.Core.Mql.Schema;

public enum FieldKind
{
    StringScalar,
    NumberScalar,
    GenreMembership
}

public sealed class FieldSchema
{
    public FieldSchema(
        string name,
        FieldKind kind,
        IReadOnlyList<MqlOperator> operators,
        string? sqlExpression = null,
        bool nullsLast = false)
    {
        NullsLast = nullsLast;
        Name = name;
        Kind = kind;
        Operators = new HashSet<MqlOperator>(operators);
        SqlExpression = sqlExpression;
    }

    public string Name { get; }

    public FieldKind Kind { get; }

    public IReadOnlySet<MqlOperator> Operators { get; }

    public string? SqlExpression { get; }

    // Sort unknown values after known ones when descending. Only for fields
    // backed by a matching DESC NULLS LAST index; elsewhere it would defeat the
    // plain index a backward scan can use.
    public bool NullsLast { get; }

    public MqlType ValueType => Kind == FieldKind.NumberScalar ? MqlType.Number : MqlType.String;

    public bool Orderable => Kind != FieldKind.GenreMembership;

    public bool Allows(MqlOperator op) => Operators.Contains(op);
}
