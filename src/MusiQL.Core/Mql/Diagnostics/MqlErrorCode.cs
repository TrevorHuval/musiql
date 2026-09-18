namespace MusiQL.Core.Mql.Diagnostics;

public enum MqlErrorCode
{
    QueryTooLong,
    UnexpectedCharacter,
    UnterminatedString,
    InvalidEscape,
    NumberOutOfRange,
    UnexpectedToken,
    ExpectedToken,
    UnknownEntity,
    UnknownField,
    OperatorNotAllowed,
    TypeMismatch,
    EmptyInList,
    LimitOutOfRange,
    TooManyClauses,
    ExpressionTooDeep,
    LibraryUserRequired,
    DuplicateOrderField
}
