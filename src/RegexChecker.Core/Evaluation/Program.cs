using RegexChecker.Core.Ast;

namespace RegexChecker.Core.Evaluation;

internal enum OpCode : byte
{
    Char,
    Any,
    Class,
    Split,
    Jmp,
    Mark,
    Progress,
    AssertStart,
    AssertEnd,
    Match,
}

internal readonly record struct Instruction(OpCode Op, int Arg, int Arg2);

internal sealed class Program
{
    public Program(Instruction[] code, CharClass[] classes, int slotCount)
    {
        Code = code;
        Classes = classes;
        SlotCount = slotCount;
    }

    public Instruction[] Code { get; }
    public CharClass[] Classes { get; }
    public int SlotCount { get; }
}
