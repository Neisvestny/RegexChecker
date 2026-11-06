using RegexChecker.Core.Ast;

namespace RegexChecker.Core.Parsing;

public sealed class PatternParser
{
    public const int MaxQuantifierValue = 10000;

    private readonly string _pattern;
    private int _pos;

    private PatternParser(string pattern) => _pattern = pattern;

    public static ParseResult Parse(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return ParseResult.Failure(new RegexSyntaxError("Регулярное выражение не задано", 0));

        var parser = new PatternParser(pattern);
        try
        {
            var root = parser.ParseAlternation();
            if (!parser.AtEnd)
            {
                throw parser.Peek == ')'
                    ? Fail("Лишняя закрывающая скобка «)»", parser._pos)
                    : Fail($"Неожиданный символ «{parser.Peek}»", parser._pos);
            }

            return ParseResult.Ok(new RegexAst(root));
        }
        catch (SyntaxErrorException ex)
        {
            return ParseResult.Failure(ex.Error);
        }
    }

    private bool AtEnd => _pos >= _pattern.Length;

    private char Peek => _pattern[_pos];

    private static SyntaxErrorException Fail(string message, int position) =>
        new(new RegexSyntaxError(message, position));

    private SyntaxErrorException ErrorHere(string message) => Fail(message, _pos);

    private RegexNode ParseAlternation()
    {
        var branches = new List<RegexNode> { ParseConcat() };
        while (!AtEnd && Peek == '|')
        {
            _pos++;
            branches.Add(ParseConcat());
        }

        return branches.Count == 1 ? branches[0] : new AlternationNode(branches);
    }

    private RegexNode ParseConcat()
    {
        var items = new List<RegexNode>();
        while (!AtEnd && Peek != '|' && Peek != ')')
            items.Add(ParseQuantified());

        return items.Count == 1 ? items[0] : new ConcatNode(items);
    }

    private RegexNode ParseQuantified()
    {
        var atom = ParseAtom();
        if (AtEnd || Peek is '|' or ')')
            return atom;

        int min;
        int max; // -1 — без верхней границы
        if (Peek is '*' or '+' or '?')
        {
            char q = Peek;
            _pos++;
            min = q == '+' ? 1 : 0;
            max = q == '?' ? 1 : -1;
        }
        else if (Peek == '{' && TryParseBraceQuantifier(out min, out max, out int consumed))
        {
            if (max != -1 && min > max)
                throw ErrorHere($"В кванторе нижняя граница ({min}) больше верхней ({max})");
            if (min > MaxQuantifierValue || max > MaxQuantifierValue)
                throw ErrorHere($"Значение квантора не может превышать {MaxQuantifierValue}");
            _pos += consumed;
        }
        else
        {
            return atom;
        }

        bool lazy = false;
        if (!AtEnd && Peek == '?')
        {
            lazy = true;
            _pos++;
        }

        if (!AtEnd && Peek is '*' or '+' or '?')
            throw ErrorHere($"Квантор «{Peek}» не может следовать за другим квантором — экранируйте его");

        if (!AtEnd && Peek == '{' && TryParseBraceQuantifier(out _, out _, out _))
            throw ErrorHere("Квантор не может следовать за другим квантором — экранируйте его");

        return new RepeatNode(atom, min, max == -1 ? null : max, lazy);
    }

    private RegexNode ParseAtom()
    {
        char c = Peek;
        switch (c)
        {
            case '(':
                return ParseGroup();
            case '[':
                return ParseCharClassNode();
            case '.':
                _pos++;
                return DotNode.Instance;
            case '^':
                _pos++;
                return AnchorStartNode.Instance;
            case '$':
                _pos++;
                return AnchorEndNode.Instance;
            case '*':
            case '+':
            case '?':
                throw ErrorHere($"Квантор «{c}» без выражения");
            case '{':
                if (TryParseBraceQuantifier(out _, out _, out _))
                    throw ErrorHere("Квантор без выражения");
                _pos++;
                return new LiteralNode('{');
            case '\\':
                return ParseEscapeNode(inClass: false);
            default:
                _pos++;
                return new LiteralNode(c);
        }
    }

    private RegexNode ParseGroup()
    {
        int openPos = _pos;
        _pos++; // '('
        bool nonCapturing = false;

        if (!AtEnd && Peek == '?')
        {
            if (_pos + 1 < _pattern.Length && _pattern[_pos + 1] == ':')
            {
                nonCapturing = true;
                _pos += 2;
            }
            else
            {
                throw Fail(DescribeUnsupportedGroup(), openPos);
            }
        }

        var inner = ParseAlternation();
        if (AtEnd)
            throw Fail("Не закрыта группа — ожидается «)»", _pattern.Length);

        _pos++; // ')'
        return nonCapturing
            ? new NonCapturingGroupNode(inner)
            : new GroupNode(inner);
    }

    private string DescribeUnsupportedGroup()
    {
        // _pos указывает на «?» после «(»
        char c2 = _pos + 1 < _pattern.Length ? _pattern[_pos + 1] : '\0';
        return c2 switch
        {
            '=' or '!' => "Проверка (lookahead) не поддерживается",
            '<' when PeekAt(_pos + 2) is '=' or '!' => "Проверка сзади (lookbehind) не поддерживается",
            '<' or 'P' => "Именованные группы не поддерживаются",
            '#' => "Комментарии внутри выражения не поддерживаются",
            _ => "Неподдерживаемая конструкция группы",
        };
    }

    private RegexNode ParseCharClassNode()
    {
        int openPos = _pos;
        _pos++; // '['
        bool negated = false;
        if (!AtEnd && Peek == '^')
        {
            negated = true;
            _pos++;
        }

        var ranges = new List<(char From, char To)>();
        bool first = true;

        while (true)
        {
            if (AtEnd)
                throw Fail("Не закрыт класс символов — ожидается «]»", _pattern.Length);

            if (Peek == ']')
            {
                if (first)
                    throw Fail("Пустой класс символов: экранируйте «]» как «\\]»", _pos);
                _pos++;
                break;
            }

            first = false;
            int elemStart = _pos;
            var element = ParseClassElement(elemStart);

            // a-z, если «-» не перед «]»
            if (!AtEnd && Peek == '-' && _pos + 1 < _pattern.Length && _pattern[_pos + 1] != ']')
            {
                _pos++; // '-'
                int toStart = _pos;
                var toElement = ParseClassElement(toStart);
                if (element.IsPredefined || toElement.IsPredefined)
                    throw Fail("Диапазон должен состоять из одиночных символов", elemStart);
                if (toElement.Char < element.Char)
                    throw Fail(
                        $"Неверный диапазон: «{element.Char}» больше «{toElement.Char}»",
                        elemStart);
                ranges.Add((element.Char, toElement.Char));
            }
            else if (element.IsPredefined)
            {
                ranges.AddRange(element.Class!.Ranges);
            }
            else
            {
                ranges.Add((element.Char, element.Char));
            }
        }

        var charClass = new CharClass(ranges);
        if (negated)
            charClass = CharClass.Complement(charClass);
        return new CharClassNode(charClass);
    }

    private readonly record struct ClassElement(bool IsPredefined, char Char, CharClass? Class);

    private ClassElement ParseClassElement(int elementStart)
    {
        if (Peek == '\\')
            return ParseEscape(inClass: true, elementStart);

        char c = Peek;
        _pos++;
        return new ClassElement(false, c, null);
    }

    private RegexNode ParseEscapeNode(bool inClass)
    {
        var element = ParseEscape(inClass, _pos);
        if (element.IsPredefined)
            return new CharClassNode(element.Class!);
        return new LiteralNode(element.Char);
    }

    private ClassElement ParseEscape(bool inClass, int escapeStart)
    {
        _pos++; // '\'
        if (AtEnd)
            throw Fail("Незавершённое экранирование — после «\\» нет символа", escapeStart);

        char c = Peek;
        switch (c)
        {
            case 'n':
                _pos++;
                return new ClassElement(false, '\n', null);
            case 'r':
                _pos++;
                return new ClassElement(false, '\r', null);
            case 't':
                _pos++;
                return new ClassElement(false, '\t', null);
            case 'f':
                _pos++;
                return new ClassElement(false, '\f', null);
            case 'v':
                _pos++;
                return new ClassElement(false, '\v', null);
            case '0':
                _pos++;
                return new ClassElement(false, '\0', null);
            case 'd':
                _pos++;
                return new ClassElement(true, default, CharClass.Digit);
            case 'D':
                _pos++;
                return new ClassElement(true, default, CharClass.NotDigit);
            case 'w':
                _pos++;
                return new ClassElement(true, default, CharClass.Word);
            case 'W':
                _pos++;
                return new ClassElement(true, default, CharClass.NotWord);
            case 's':
                _pos++;
                return new ClassElement(true, default, CharClass.Space);
            case 'S':
                _pos++;
                return new ClassElement(true, default, CharClass.NotSpace);
            case 'b':
                throw Fail("Словесная граница «\\b» не поддерживается", escapeStart);
            case 'A' or 'Z' or 'G' or 'K' or 'z':
                throw Fail($"Конструкция «\\{c}» не поддерживается", escapeStart);
            case >= '1' and <= '9':
                throw Fail("Обратные ссылки не поддерживаются", escapeStart);
            default:
                if (char.IsAsciiLetterOrDigit(c))
                    throw Fail($"Неизвестная последовательность экранирования «\\{c}»", escapeStart);
                _pos++;
                return new ClassElement(false, c, null);
        }
    }

    private bool TryParseBraceQuantifier(out int min, out int max, out int consumed)
    {
        min = 0;
        max = 0;
        consumed = 0;

        int i = _pos + 1; // после '{'
        if (i >= _pattern.Length || !char.IsAsciiDigit(_pattern[i]))
            return false;

        int first = 0;
        while (i < _pattern.Length && char.IsAsciiDigit(_pattern[i]))
        {
            first = Math.Min((first * 10) + (_pattern[i] - '0'), MaxQuantifierValue + 1);
            i++;
        }

        int second;
        bool unbounded = false;

        if (i < _pattern.Length && _pattern[i] == ',')
        {
            i++;
            if (i < _pattern.Length && char.IsAsciiDigit(_pattern[i]))
            {
                second = 0;
                while (i < _pattern.Length && char.IsAsciiDigit(_pattern[i]))
                {
                    second = Math.Min((second * 10) + (_pattern[i] - '0'), MaxQuantifierValue + 1);
                    i++;
                }
            }
            else
            {
                second = -1;
                unbounded = true;
            }
        }
        else
        {
            second = first;
        }

        if (i >= _pattern.Length || _pattern[i] != '}')
            return false; // не квантор — это литерал {…}

        min = first;
        max = unbounded ? -1 : second;
        consumed = (i + 1) - _pos;
        return true;
    }

    private char PeekAt(int index) =>
        index >= 0 && index < _pattern.Length ? _pattern[index] : '\0';
}
