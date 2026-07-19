namespace MusiQL.Core.Mql.Schema;

public enum MqlOperator
{
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    In,
    Between,
    Contains
}

public static class MqlOperatorText
{
    public static string Describe(MqlOperator op) => op switch
    {
        MqlOperator.Equal => "=",
        MqlOperator.NotEqual => "!=",
        MqlOperator.Less => "<",
        MqlOperator.LessEqual => "<=",
        MqlOperator.Greater => ">",
        MqlOperator.GreaterEqual => ">=",
        MqlOperator.In => "in",
        MqlOperator.Between => "between",
        MqlOperator.Contains => "contains",
        _ => op.ToString()
    };
}
