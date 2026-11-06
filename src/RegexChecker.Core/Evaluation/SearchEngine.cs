namespace RegexChecker.Core.Evaluation;

internal static class SearchEngine
{
    // пустые совпадения не включаются, но сдвигают позицию поиска на 1 символ
    public static List<MatchInfo> FindAll(Program program, string text, out bool limitExceeded)
    {
        var vm = new BacktrackingVm(program);
        var matches = new List<MatchInfo>();
        int start = 0;

        while (start <= text.Length)
        {
            if (vm.TryMatch(text, start, out int end))
            {
                if (end > start)
                {
                    matches.Add(new MatchInfo(start, end - start));
                    start = end;
                }
                else
                {
                    start++;
                }
            }
            else
            {
                if (vm.LimitExceeded)
                {
                    limitExceeded = true;
                    return matches;
                }

                start++;
            }
        }

        limitExceeded = false;
        return matches;
    }
}
