namespace MusiQL.Core.Mql.Diagnostics;

public enum MqlErrorCode
{
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
