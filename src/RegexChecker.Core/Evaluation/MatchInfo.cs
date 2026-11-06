namespace RegexChecker.Core.Evaluation;

public readonly record struct MatchInfo(int Start, int Length)
{
    public int End => Start + Length;

    public string Value(string text) => text.Substring(Start, Length);
}
