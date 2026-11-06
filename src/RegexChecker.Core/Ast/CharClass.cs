namespace RegexChecker.Core.Ast;

public sealed class CharClass
{
    private readonly (char From, char To)[] _ranges;

    public CharClass(IEnumerable<(char From, char To)> ranges)
    {
        _ranges = Normalize(ranges);
    }

    public IReadOnlyList<(char From, char To)> Ranges => _ranges;

    public static CharClass Digit { get; } = new(new[] { ('0', '9') });

    public static CharClass Word { get; } = new(new[]
    {
        ('0', '9'), ('A', 'Z'), ('_', '_'), ('a', 'z'),
    });

    public static CharClass Space { get; } = new(new[]
    {
        ('\t', '\t'), ('\n', '\n'), ('\v', '\v'), ('\f', '\f'), ('\r', '\r'), (' ', ' '),
    });

    public static CharClass NotDigit { get; } = Complement(Digit);

    public static CharClass NotWord { get; } = Complement(Word);

    public static CharClass NotSpace { get; } = Complement(Space);

    public static CharClass Complement(CharClass charClass)
    {
        var result = new List<(char From, char To)>();
        int next = 0;
        foreach (var (from, to) in charClass._ranges)
        {
            if (from > next)
                result.Add(((char)next, (char)(from - 1)));
            next = to + 1;
        }

        if (next <= char.MaxValue)
            result.Add(((char)next, char.MaxValue));
        return new CharClass(result);
    }

    public static CharClass Union(IEnumerable<CharClass> classes)
    {
        var ranges = new List<(char From, char To)>();
        foreach (var c in classes)
            ranges.AddRange(c._ranges);
        return new CharClass(ranges);
    }

    public bool Matches(char c)
    {
        int lo = 0;
        int hi = _ranges.Length - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            var (from, to) = _ranges[mid];
            if (c < from)
                hi = mid - 1;
            else if (c > to)
                lo = mid + 1;
            else
                return true;
        }

        return false;
    }

    private static (char From, char To)[] Normalize(IEnumerable<(char From, char To)> ranges)
    {
        var list = ranges.Where(r => r.From <= r.To).OrderBy(r => r.From).ToList();
        if (list.Count == 0)
            return Array.Empty<(char, char)>();

        var merged = new List<(char From, char To)> { list[0] };
        for (int i = 1; i < list.Count; i++)
        {
            var last = merged[^1];
            var current = list[i];
            if (current.From <= last.To + 1)
                merged[^1] = (last.From, (char)Math.Max(last.To, current.To));
            else
                merged.Add(current);
        }

        return merged.ToArray();
    }
}
