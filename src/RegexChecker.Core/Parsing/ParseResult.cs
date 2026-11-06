using RegexChecker.Core.Ast;

namespace RegexChecker.Core.Parsing;

public sealed record ParseResult
{
    private ParseResult(bool success, RegexAst? ast, RegexSyntaxError? error)
    {
        Success = success;
        Ast = ast;
        Error = error;
    }

    public bool Success { get; }
    public RegexAst? Ast { get; }
    public RegexSyntaxError? Error { get; }

    public static ParseResult Ok(RegexAst ast) => new(true, ast, null);

    public static ParseResult Failure(RegexSyntaxError error) => new(false, null, error);
}
