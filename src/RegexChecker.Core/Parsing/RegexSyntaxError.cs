namespace RegexChecker.Core.Parsing;

public sealed record RegexSyntaxError(string Message, int Position);

internal sealed class SyntaxErrorException : Exception
{
    public SyntaxErrorException(RegexSyntaxError error)
        : base(error.Message) => Error = error;

    public RegexSyntaxError Error { get; }
}
