namespace MusiQL.Api.Query;

public static class ColumnTypes
{
    public static string Of(Type type)
    {
        var inner = Nullable.GetUnderlyingType(type) ?? type;
        if (inner == typeof(Guid))
        {
            return "guid";
        }

        if (inner == typeof(string))
        {
            return "string";
        }

        if (inner == typeof(short) || inner == typeof(int) || inner == typeof(long))
        {
            return "number";
        }

        return inner.Name.ToLowerInvariant();
    }
}
