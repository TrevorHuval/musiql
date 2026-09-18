using System.Text;
using MusiQL.Core.Mql.Diagnostics;

namespace MusiQL.Core.Mql.Lexing;

public sealed class Lexer(string source)
{
    private static readonly IReadOnlyDictionary<string, TokenKind> Keywords =
        new Dictionary<string, TokenKind>(StringComparer.Ordinal)
        {
            ["where"] = TokenKind.Where,
            ["order"] = TokenKind.Order,
            ["by"] = TokenKind.By,
            ["limit"] = TokenKind.Limit,
            ["and"] = TokenKind.And,
            ["or"] = TokenKind.Or,
            ["not"] = TokenKind.Not,
            ["in"] = TokenKind.In,
            ["between"] = TokenKind.Between,
            ["contains"] = TokenKind.Contains,
            ["asc"] = TokenKind.Asc,
            ["desc"] = TokenKind.Desc,
            ["from"] = TokenKind.From,
            ["library"] = TokenKind.Library,
        };

    private readonly string _source = source;
    private int _pos;

    public IReadOnlyList<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            var token = Next();
            tokens.Add(token);
            if (token.Kind == TokenKind.End)
            {
                return tokens;
            }

            if (tokens.Count > MqlLimits.MaxTokens)
            {
                throw new MqlException(new MqlError(
                    MqlErrorCode.QueryTooLong,
                    $"Queries are limited to {MqlLimits.MaxTokens:N0} tokens.",
                    token.Span));
            }
        }
    }

    private Token Next()
    {
        SkipWhitespace();
        if (_pos >= _source.Length)
        {
            return new Token(TokenKind.End, new TextSpan(_source.Length, 0), "");
        }

        var start = _pos;
        var c = _source[_pos];

        if (c == '"')
        {
            return LexString();
        }

        if (IsDigit(c))
        {
            return LexNumber();
        }

        if (IsIdentifierStart(c))
        {
            return LexWord();
        }

        _pos++;
        return c switch
        {
            '(' => Punctuation(TokenKind.LeftParen, start),
            ')' => Punctuation(TokenKind.RightParen, start),
            ',' => Punctuation(TokenKind.Comma, start),
            '=' => Punctuation(TokenKind.Equal, start),
            '<' => LexLessOrLessEqual(start),
            '>' => LexGreaterOrGreaterEqual(start),
            '!' => LexNotEqual(start),
            _ => throw Unexpected(start, c)
        };
    }

    private Token LexLessOrLessEqual(int start)
    {
        if (Peek() == '=')
        {
            _pos++;
            return Punctuation(TokenKind.LessEqual, start);
        }

        return Punctuation(TokenKind.Less, start);
    }

    private Token LexGreaterOrGreaterEqual(int start)
    {
        if (Peek() == '=')
        {
            _pos++;
            return Punctuation(TokenKind.GreaterEqual, start);
        }

        return Punctuation(TokenKind.Greater, start);
    }

    private Token LexNotEqual(int start)
    {
        if (Peek() == '=')
        {
            _pos++;
            return Punctuation(TokenKind.NotEqual, start);
        }

        throw new MqlException(new MqlError(
            MqlErrorCode.UnexpectedCharacter,
            "'!' must be part of '!='.",
            new TextSpan(start, 1)));
    }

    private Token LexWord()
    {
        var start = _pos;
        while (_pos < _source.Length && IsIdentifierPart(_source[_pos]))
        {
            _pos++;
        }

        var span = TextSpan.FromBounds(start, _pos);
        var text = _source.Substring(start, span.Length);
        var kind = Keywords.GetValueOrDefault(text.ToLowerInvariant(), TokenKind.Identifier);
        return new Token(kind, span, text);
    }

    private Token LexNumber()
    {
        var start = _pos;
        while (_pos < _source.Length && IsDigit(_source[_pos]))
        {
            _pos++;
        }

        if (_pos < _source.Length && IsIdentifierStart(_source[_pos]))
        {
            throw Unexpected(_pos, _source[_pos]);
        }

        var span = TextSpan.FromBounds(start, _pos);
        var text = _source.Substring(start, span.Length);
        if (!long.TryParse(text, out var value))
        {
            throw new MqlException(new MqlError(
                MqlErrorCode.NumberOutOfRange,
                $"'{text}' is too large.",
                span));
        }

        return new Token(TokenKind.Number, span, text, value);
    }

    private Token LexString()
    {
        var start = _pos;
        _pos++;
        var builder = new StringBuilder();
        while (_pos < _source.Length)
        {
            var c = _source[_pos];
            if (c == '"')
            {
                _pos++;
                var span = TextSpan.FromBounds(start, _pos);
                return new Token(TokenKind.String, span, builder.ToString());
            }

            if (c == '\\')
            {
                builder.Append(ReadEscape(start));
                continue;
            }

            builder.Append(c);
            _pos++;
        }

        throw new MqlException(new MqlError(
            MqlErrorCode.UnterminatedString,
            "String is missing a closing quote.",
            TextSpan.FromBounds(start, _pos)));
    }

    private char ReadEscape(int stringStart)
    {
        var escapeStart = _pos;
        _pos++;
        if (_pos >= _source.Length)
        {
            throw new MqlException(new MqlError(
                MqlErrorCode.UnterminatedString,
                "String is missing a closing quote.",
                TextSpan.FromBounds(stringStart, _pos)));
        }

        var c = _source[_pos];
        _pos++;
        return c switch
        {
            '"' => '"',
            '\\' => '\\',
            'n' => '\n',
            't' => '\t',
            'r' => '\r',
            _ => throw new MqlException(new MqlError(
                MqlErrorCode.InvalidEscape,
                $"Unknown escape '\\{c}'.",
                TextSpan.FromBounds(escapeStart, _pos)))
        };
    }

    private Token Punctuation(TokenKind kind, int start) =>
        new(kind, TextSpan.FromBounds(start, _pos), _source.Substring(start, _pos - start));

    private void SkipWhitespace()
    {
        while (_pos < _source.Length && char.IsWhiteSpace(_source[_pos]))
        {
            _pos++;
        }
    }

    private char Peek() => _pos < _source.Length ? _source[_pos] : '\0';

    private static MqlException Unexpected(int at, char c)
    {
        var display = char.IsControl(c) ? $"U+{(int)c:X4}" : c.ToString();
        return new MqlException(new MqlError(
            MqlErrorCode.UnexpectedCharacter,
            $"Unexpected character '{display}'.",
            new TextSpan(at, 1)));
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    private static bool IsIdentifierStart(char c) =>
        c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_';

    private static bool IsIdentifierPart(char c) => IsIdentifierStart(c) || IsDigit(c);
}
