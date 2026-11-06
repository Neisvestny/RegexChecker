using RegexChecker.Core.Ast;

namespace RegexChecker.Core.Evaluation;

internal sealed class ComplexityException : Exception
{
    public ComplexityException(string message)
        : base(message)
    {
    }
}

internal sealed class ProgramBuilder : INodeVisitor<int>
{
    public const int MaxProgramSize = 100_000;

    private readonly List<Instruction> _code = new();
    private readonly List<CharClass> _classes = new();
    private int _slotCount;

    public static Program Compile(RegexAst ast)
    {
        var builder = new ProgramBuilder();
        ast.Root.Accept(builder);
        builder.Emit(OpCode.Match);
        return builder.Build();
    }

    public Program Build() => new(_code.ToArray(), _classes.ToArray(), _slotCount);

    public int VisitLiteral(LiteralNode node) => Emit(OpCode.Char, node.Value);

    public int VisitDot(DotNode node) => Emit(OpCode.Any);

    public int VisitCharClass(CharClassNode node)
    {
        int index = _classes.Count;
        _classes.Add(node.Class);
        return Emit(OpCode.Class, index);
    }

    public int VisitConcat(ConcatNode node)
    {
        int start = Mark();
        foreach (var child in node.Children)
            child.Accept(this);
        return start;
    }

    public int VisitAlternation(AlternationNode node)
    {
        int start = Mark();
        var branches = node.Branches;
        if (branches.Count == 1)
        {
            branches[0].Accept(this);
            return start;
        }

        var jumpIndices = new List<int>();
        for (int i = 0; i < branches.Count - 1; i++)
        {
            int split = Emit(OpCode.Split);
            PatchArg(split, split + 1); // предпочитаем тело текущей ветви
            branches[i].Accept(this);
            int jmp = Emit(OpCode.Jmp);
            // альтернатива — после ветви
            PatchArg2(split, jmp + 1);
            jumpIndices.Add(jmp);
        }

        branches[^1].Accept(this); // последняя ветвь
        int end = Mark();
        foreach (var j in jumpIndices)
            PatchArg(j, end);
        return start;
    }

    public int VisitRepeat(RepeatNode node)
    {
        int start = Mark();

        // обязательные повторения просто раскрываются
        for (int i = 0; i < node.Min; i++)
            node.Inner.Accept(this);

        if (node.Max is null)
        {
            // цикл с защитой от пустых итераций (Mark/Progress)
            int loop = Mark();
            int split = Emit(OpCode.Split);
            int body = Mark();
            int slot = _slotCount++;
            Emit(OpCode.Mark, slot);
            node.Inner.Accept(this);
            int progress = Emit(OpCode.Progress, slot);
            Emit(OpCode.Jmp, loop);
            int exit = Mark();

            if (node.Lazy)
            {
                PatchArg(split, exit); // ленивый: сначала выход
                PatchArg2(split, body);
            }
            else
            {
                PatchArg(split, body); // жадный: сначала продолжение
                PatchArg2(split, exit);
            }

            PatchArg2(progress, exit);
        }
        else
        {
            // ограничение: max - min необязательных копий через Split
            int optional = node.Max.Value - node.Min;
            var splits = new List<int>();
            for (int i = 0; i < optional; i++)
            {
                int split = Emit(OpCode.Split);
                PatchArg(split, split + 1); // тело сразу за Split
                splits.Add(split);
                node.Inner.Accept(this);
            }

            int end = Mark();
            foreach (var s in splits)
            {
                if (node.Lazy)
                {
                    // ленивый: сначала выход
                    PatchArg(s, end);
                    PatchArg2(s, s + 1);
                }
                else
                {
                    // жадный: сначала тело
                    PatchArg2(s, end);
                }
            }
        }

        return start;
    }

    public int VisitAnchorStart(AnchorStartNode node) => Emit(OpCode.AssertStart);

    public int VisitAnchorEnd(AnchorEndNode node) => Emit(OpCode.AssertEnd);

    public int VisitGroup(GroupNode node) => node.Inner.Accept(this);

    public int VisitNonCapturingGroup(NonCapturingGroupNode node) => node.Inner.Accept(this);

    private int Mark() => _code.Count;

    private int Emit(OpCode op, int arg = 0, int arg2 = 0)
    {
        if (_code.Count >= MaxProgramSize)
        {
            throw new ComplexityException(
                "Выражение слишком большое — упростите его (допустимо не более 100 000 инструкций).");
        }

        _code.Add(new Instruction(op, arg, arg2));
        return _code.Count - 1;
    }

    private void PatchArg(int index, int value)
    {
        var instruction = _code[index];
        _code[index] = instruction with { Arg = value };
    }

    private void PatchArg2(int index, int value)
    {
        var instruction = _code[index];
        _code[index] = instruction with { Arg2 = value };
    }
}
