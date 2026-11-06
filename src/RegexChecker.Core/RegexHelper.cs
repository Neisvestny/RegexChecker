using RegexChecker.Core.Evaluation;
using RegexChecker.Core.Parsing;

namespace RegexChecker.Core;

public abstract record SearchOutcome;

public sealed record SearchSuccess(IReadOnlyList<MatchInfo> Matches) : SearchOutcome;

public sealed record SearchInvalidExpression(RegexSyntaxError Error) : SearchOutcome;

public sealed record SearchFailed(string Message) : SearchOutcome;

public static class RegexHelper
{
    public static ParseResult Validate(string? pattern) => PatternParser.Parse(pattern);

    public static SearchOutcome Search(string? pattern, string? text)
    {
        var parse = PatternParser.Parse(pattern);
        if (!parse.Success)
            return new SearchInvalidExpression(parse.Error!);

        text ??= string.Empty;

        Program program;
        try
        {
            program = ProgramBuilder.Compile(parse.Ast!);
        }
        catch (ComplexityException ex)
        {
            return new SearchFailed(ex.Message);
        }

        var matches = SearchEngine.FindAll(program, text, out bool limitExceeded);
        if (limitExceeded)
        {
            return new SearchFailed(
                "Выражение слишком ресурсоёмкое для этой строки — проверка прервана по лимиту шагов.");
        }

        return new SearchSuccess(matches);
    }
}
