namespace RegexChecker.Core.Ast;

public sealed class RegexAst
{
    public RegexAst(RegexNode root) => Root = root;

    public RegexNode Root { get; }
}
