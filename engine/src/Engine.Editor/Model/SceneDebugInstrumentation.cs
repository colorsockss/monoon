using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine.Editor;

// Read original source coordinates and symbols before inserting a hook ahead of each block statement.
// No trivia with newlines is added, so stack traces retain their original source line numbers.
internal sealed class SceneDebugInstrumentation(SemanticModel model, HashSet<int> lines) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitBlock(BlockSyntax block)
    {
        var owner = block.Ancestors().FirstOrDefault(n => n is BaseMethodDeclarationSyntax or
            AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax);
        if (owner is not MethodDeclarationSyntax method || model.GetDeclaredSymbol(method) is not IMethodSymbol symbol)
            return base.VisitBlock(block);

        var statements = new List<StatementSyntax>();
        foreach (var statement in block.Statements)
        {
            var visited = (StatementSyntax)Visit(statement)!;
            if (statement is BlockSyntax or LocalFunctionStatementSyntax or EmptyStatementSyntax or LabeledStatementSyntax)
            {
                statements.Add(visited);
                continue;
            }
            int line = statement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            string file = SymbolDisplay.FormatLiteral(model.SyntaxTree.FilePath, true);
            string methodName = SymbolDisplay.FormatLiteral(symbol.ToDisplayString(), true);
            var names = new List<string>();
            var expressions = new List<string>();
            void Add(string name, string expression) { names.Add(SymbolDisplay.FormatLiteral(name, true)); expressions.Add(expression); }
            if (!symbol.IsStatic && !symbol.ContainingType.IsRefLikeType) Add("this", "this");
            var assigned = model.AnalyzeDataFlow(statement)?.DefinitelyAssignedOnEntry ?? [];
            foreach (var item in model.LookupSymbols(statement.SpanStart))
            {
                ITypeSymbol? type = item switch {
                    IParameterSymbol p when SymbolEqualityComparer.Default.Equals(p.ContainingSymbol, symbol) &&
                        (p.RefKind != RefKind.Out || assigned.Contains(p, SymbolEqualityComparer.Default)) => p.Type,
                    ILocalSymbol l when l.CanBeReferencedByName && assigned.Contains(l, SymbolEqualityComparer.Default) => l.Type,
                    _ => null
                };
                if (type == null || type.IsRefLikeType || type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer ||
                    type is ITypeParameterSymbol { AllowsRefLikeType: true } || type.SpecialType == SpecialType.System_TypedReference) continue;
                Add(item.Name, "@" + item.Name);
            }
            string bridge = "global::Engine.Core.SceneScriptBreakpoint";
            var hook = SyntaxFactory.ParseStatement(
                $"if({bridge}.IsSet({file},{line})) {bridge}.Hit({file},{line},{methodName},new string[]{{{string.Join(",",names)}}},new object[]{{{string.Join(",",expressions)}}});");
            lines.Add(line);
            statements.Add(hook.WithLeadingTrivia(visited.GetLeadingTrivia()));
            statements.Add(visited.WithoutLeadingTrivia());
        }
        return block.WithStatements(SyntaxFactory.List(statements));
    }
}
