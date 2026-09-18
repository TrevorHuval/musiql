namespace MusiQL.Core.Mql;

public static class MqlLimits
{
    public const int MaxSourceLength = 8000;
    public const int MaxTokens = 2000;
    public const int MaxExpressionDepth = 32;
    public const int MaxPredicates = 40;
    public const int MaxInListItems = 100;
    public const int MaxOrderKeys = 8;
    public const int MaxRowCap = 500;
    public const int DefaultRowCap = 100;
}
