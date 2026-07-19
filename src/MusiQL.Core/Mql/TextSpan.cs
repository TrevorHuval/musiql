namespace MusiQL.Core.Mql;

public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;

    public static TextSpan FromBounds(int start, int end) => new(start, end - start);

    public TextSpan To(TextSpan other) => FromBounds(Start, other.End);
}
