namespace RegexChecker.Core.Evaluation;

// явный стек отката вместо рекурсии + бюджет шагов против катастрофического перебора
internal sealed class BacktrackingVm
{
    public const long MaxTotalSteps = 20_000_000;

    private readonly Program _program;
    private readonly Stack<(int Pc, int Sp)> _stack = new();
    private readonly int[] _slots;

    private string _input = "";
    private long _steps;
    private int _pc;
    private int _sp;

    public BacktrackingVm(Program program)
    {
        _program = program;
        _slots = new int[program.SlotCount];
    }

    public bool LimitExceeded { get; private set; }

    public bool TryMatch(string input, int start, out int end)
    {
        _input = input;
        _stack.Clear();
        _pc = 0;
        _sp = start;
        end = 0;

        while (true)
        {
            if (++_steps > MaxTotalSteps)
            {
                LimitExceeded = true;
                return false;
            }

            var instruction = _program.Code[_pc];
            switch (instruction.Op)
            {
                case OpCode.Char:
                    if (_sp < _input.Length && _input[_sp] == instruction.Arg)
                    {
                        _sp++;
                        _pc++;
                    }
                    else if (!Backtrack())
                    {
                        return false;
                    }

                    break;

                case OpCode.Any:
                    if (_sp < _input.Length && _input[_sp] != '\n')
                    {
                        _sp++;
                        _pc++;
                    }
                    else if (!Backtrack())
                    {
                        return false;
                    }

                    break;

                case OpCode.Class:
                    if (_sp < _input.Length && _program.Classes[instruction.Arg].Matches(_input[_sp]))
                    {
                        _sp++;
                        _pc++;
                    }
                    else if (!Backtrack())
                    {
                        return false;
                    }

                    break;

                case OpCode.Split:
                    _stack.Push((instruction.Arg2, _sp));
                    _pc = instruction.Arg;
                    break;

                case OpCode.Jmp:
                    _pc = instruction.Arg;
                    break;

                case OpCode.Mark:
                    _slots[instruction.Arg] = _sp;
                    _pc++;
                    break;

                case OpCode.Progress:
                    _pc = _slots[instruction.Arg] == _sp ? instruction.Arg2 : _pc + 1;
                    break;

                case OpCode.AssertStart:
                    if (_sp == 0)
                        _pc++;
                    else if (!Backtrack())
                        return false;
                    break;

                case OpCode.AssertEnd:
                    if (_sp == _input.Length)
                        _pc++;
                    else if (!Backtrack())
                        return false;
                    break;

                case OpCode.Match:
                    end = _sp;
                    return true;

                default:
                    throw new InvalidOperationException($"Неизвестная инструкция: {instruction.Op}");
            }
        }
    }

    private bool Backtrack()
    {
        if (_stack.Count == 0)
            return false;

        var (pc, sp) = _stack.Pop();
        _pc = pc;
        _sp = sp;
        return true;
    }
}
