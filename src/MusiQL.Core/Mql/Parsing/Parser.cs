using MusiQL.Core.Mql.Ast;
using MusiQL.Core.Mql.Diagnostics;
using MusiQL.Core.Mql.Lexing;

namespace MusiQL.Core.Mql.Parsing;

public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _index;
    private int _depth;

    private Parser(IReadOnlyList<Token> tokens) => _tokens = tokens;

    public static MqlQuery Parse(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        return new Parser(tokens).ParseQuery();
    }

    private MqlQuery ParseQuery()
    {
        var entity = ParseEntity();

        var fromLibrary = false;
        if (Current.Kind == TokenKind.From)
        {
            Advance();
            Expect(TokenKind.Library);
            fromLibrary = true;
        }

        MqlExpr? where = null;
        if (Current.Kind == TokenKind.Where)
        {
            Advance();
            where = ParseExpr();
        }

        var orderBy = Array.Empty<OrderKey>() as IReadOnlyList<OrderKey>;
        if (Current.Kind == TokenKind.Order)
        {
            Advance();
            Expect(TokenKind.By);
            orderBy = ParseOrderList();
        }

        int? limit = null;
        var limitSpan = default(TextSpan);
        if (Current.Kind == TokenKind.Limit)
        {
            Advance();
            var number = Expect(TokenKind.Number);
            limitSpan = number.Span;
            limit = ClampLimitToken(number);
        }

        var end = Expect(TokenKind.End);
        var span = TextSpan.FromBounds(entity.Span.Start, end.Span.Start);
        return new MqlQuery(entity, fromLibrary, where, orderBy, limit, limitSpan, span);
    }

    private EntityRef ParseEntity()
    {
        var token = Expect(TokenKind.Identifier, "an entity name (tracks, albums, or artists)");
        return new EntityRef(token.Text, token.Span);
    }

    private IReadOnlyList<OrderKey> ParseOrderList()
    {
        var keys = new List<OrderKey>();
        while (true)
        {
            keys.Add(ParseOrderKey());
            if (Current.Kind != TokenKind.Comma)
            {
                return keys;
            }

            Advance();
        }
    }

    private OrderKey ParseOrderKey()
    {
        var field = ParseFieldRef();
        var descending = false;
        var end = field.Span;
        if (Current.Kind == TokenKind.Asc)
        {
            end = Current.Span;
            Advance();
        }
        else if (Current.Kind == TokenKind.Desc)
        {
            descending = true;
            end = Current.Span;
            Advance();
        }

        return new OrderKey(field, descending, field.Span.To(end));
    }

    private MqlExpr ParseExpr()
    {
        EnterDepth();
        var expr = ParseOr();
        _depth--;
        return expr;
    }

    private MqlExpr ParseOr()
    {
        var left = ParseAnd();
        while (Current.Kind == TokenKind.Or)
        {
            Advance();
            var right = ParseAnd();
            left = new OrExpr(left, right, left.Span.To(right.Span));
        }

        return left;
    }

    private MqlExpr ParseAnd()
    {
        var left = ParseNot();
        while (Current.Kind == TokenKind.And)
        {
            Advance();
            var right = ParseNot();
            left = new AndExpr(left, right, left.Span.To(right.Span));
        }

        return left;
    }

    private MqlExpr ParseNot()
    {
        if (Current.Kind == TokenKind.Not)
        {
            var start = Current.Span;
            Advance();
            EnterDepth();
            var operand = ParseNot();
            _depth--;
            return new NotExpr(operand, start.To(operand.Span));
        }

        return ParsePrimary();
    }

    private MqlExpr ParsePrimary()
    {
        if (Current.Kind == TokenKind.LeftParen)
        {
            Advance();
            var inner = ParseExpr();
            Expect(TokenKind.RightParen);
            return inner;
        }

        return ParsePredicate();
    }

    private MqlExpr ParsePredicate()
    {
        var field = ParseFieldRef();
        return Current.Kind switch
        {
            TokenKind.Equal => Comparison(field, ComparisonOp.Equal),
            TokenKind.NotEqual => Comparison(field, ComparisonOp.NotEqual),
            TokenKind.Less => Comparison(field, ComparisonOp.Less),
            TokenKind.LessEqual => Comparison(field, ComparisonOp.LessEqual),
            TokenKind.Greater => Comparison(field, ComparisonOp.Greater),
            TokenKind.GreaterEqual => Comparison(field, ComparisonOp.GreaterEqual),
            TokenKind.In => ParseIn(field),
            TokenKind.Between => ParseBetween(field),
            TokenKind.Contains => ParseContains(field),
            _ => throw Unexpected(new[]
            {
                "'='", "'!='", "'<'", "'<='", "'>'", "'>='", "'in'", "'between'", "'contains'"
            })
        };
    }

    private MqlExpr Comparison(FieldRef field, ComparisonOp op)
    {
        Advance();
        var value = ParseLiteral();
        return new ComparisonExpr(field, op, value, field.Span.To(value.Span));
    }

    private MqlExpr ParseIn(FieldRef field)
    {
        Advance();
        Expect(TokenKind.LeftParen);
        var values = new List<MqlLiteral> { ParseLiteral() };
        while (Current.Kind == TokenKind.Comma)
        {
            Advance();
            values.Add(ParseLiteral());
        }

        var close = Expect(TokenKind.RightParen);
        return new InExpr(field, values, field.Span.To(close.Span));
    }

    private MqlExpr ParseBetween(FieldRef field)
    {
        Advance();
        var low = ParseLiteral();
        Expect(TokenKind.And);
        var high = ParseLiteral();
        return new BetweenExpr(field, low, high, field.Span.To(high.Span));
    }

    private MqlExpr ParseContains(FieldRef field)
    {
        Advance();
        var value = ExpectString();
        return new ContainsExpr(field, value, field.Span.To(value.Span));
    }

    private FieldRef ParseFieldRef()
    {
        var token = Expect(TokenKind.Identifier, "a field name");
        return new FieldRef(token.Text, token.Span);
    }

    private MqlLiteral ParseLiteral()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.String:
                Advance();
                return new StringLiteral(token.Text, token.Span);
            case TokenKind.Number:
                Advance();
                return new NumberLiteral(token.Number, token.Span);
            default:
                throw Unexpected(new[] { "a quoted value", "a number" });
        }
    }

    private StringLiteral ExpectString()
    {
        var token = Current;
        if (token.Kind != TokenKind.String)
        {
            throw Unexpected(new[] { "a quoted value" });
        }

        Advance();
        return new StringLiteral(token.Text, token.Span);
    }

    private static int ClampLimitToken(Token number) =>
        number.Number is < 0 or > int.MaxValue ? int.MaxValue : (int)number.Number;

    private Token Current => _tokens[_index];

    private void Advance()
    {
        if (_index < _tokens.Count - 1)
        {
            _index++;
        }
    }

    private Token Expect(TokenKind kind, string? description = null)
    {
        if (Current.Kind == kind)
        {
            var token = Current;
            Advance();
            return token;
        }

        throw Unexpected(new[] { description ?? Token.Describe(kind) });
    }

    private void EnterDepth()
    {
        if (++_depth > MqlLimits.MaxExpressionDepth)
        {
            throw new MqlException(new MqlError(
                MqlErrorCode.ExpressionTooDeep,
                $"Expression nesting exceeds the limit of {MqlLimits.MaxExpressionDepth}.",
                Current.Span));
        }
    }

    private MqlException Unexpected(IReadOnlyList<string> expected)
    {
        var found = Current.Kind == TokenKind.End
            ? "the query ended early"
            : $"found {Token.Describe(Current.Kind)}";
        var message = $"Expected {Join(expected)} but {found}.";
        return new MqlException(new MqlError(
            MqlErrorCode.UnexpectedToken,
            message,
            Current.Span,
            expected));
    }

    private static string Join(IReadOnlyList<string> options) =>
        options.Count == 1 ? options[0] : string.Join(", ", options.Take(options.Count - 1)) + " or " + options[^1];
}
