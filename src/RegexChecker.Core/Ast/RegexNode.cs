namespace RegexChecker.Core.Ast;

public interface INodeVisitor<out T>
{
    T VisitLiteral(LiteralNode node);
    T VisitDot(DotNode node);
    T VisitCharClass(CharClassNode node);
    T VisitConcat(ConcatNode node);
    T VisitAlternation(AlternationNode node);
    T VisitRepeat(RepeatNode node);
    T VisitAnchorStart(AnchorStartNode node);
    T VisitAnchorEnd(AnchorEndNode node);
    T VisitGroup(GroupNode node);
    T VisitNonCapturingGroup(NonCapturingGroupNode node);
}

public abstract class RegexNode
{
    public abstract T Accept<T>(INodeVisitor<T> visitor);
}

public sealed class LiteralNode : RegexNode
{
    public LiteralNode(char value) => Value = value;

    public char Value { get; }

    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.VisitLiteral(this);
}

public sealed class DotNode : RegexNode
{
    public static readonly DotNode Instance = new();

    private DotNode()
    {
    }

    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.VisitDot(this);
}

public sealed class CharClassNode : RegexNode
{
    public CharClassNode(CharClass charClass) => Class = charClass;

    public CharClass Class { get; }

    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.VisitCharClass(this);
}

public sealed class ConcatNode : RegexNode
{
    public ConcatNode(IReadOnlyList<RegexNode> children) => Children = children;

    public IReadOnlyList<RegexNode> Children { get; }

    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.VisitConcat(this);
}

public sealed class AlternationNode : RegexNode
{
    public AlternationNode(IReadOnlyList<RegexNode> branches) => Branches = branches;

    public IReadOnlyList<RegexNode> Branches { get; }

    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.VisitAlternation(this);
}

public sealed class RepeatNode : RegexNode
{
    public RepeatNode(RegexNode inner, int min, int? max, bool lazy)
    {
        Inner = inner;
        Min = min;
        Max = max;
        Lazy = lazy;
    }

    public RegexNode Inner { get; }
    public int Min { get; }
    public int? Max { get; }
    public bool Lazy { get; }

    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.VisitRepeat(this);
}

public sealed class AnchorStartNode : RegexNode
{
    public static readonly AnchorStartNode Instance = new();

    private AnchorStartNode()
    {
    }

    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.VisitAnchorStart(this);
}

public sealed class AnchorEndNode : RegexNode
{
    public static readonly AnchorEndNode Instance = new();

    private AnchorEndNode()
    {
    }

    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.VisitAnchorEnd(this);
}

public sealed class GroupNode : RegexNode
{
    public GroupNode(RegexNode inner) => Inner = inner;

    public RegexNode Inner { get; }

    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.VisitGroup(this);
}

public sealed class NonCapturingGroupNode : RegexNode
{
    public NonCapturingGroupNode(RegexNode inner) => Inner = inner;

    public RegexNode Inner { get; }

    public override T Accept<T>(INodeVisitor<T> visitor) => visitor.VisitNonCapturingGroup(this);
}
