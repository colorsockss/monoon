using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Engine.Core;

public sealed partial class ProjectScripts
{
    // Engine features the scripts can reach: every name in the sources is resolved, so calls, member and type references,
    // generic arguments (GetComponent<Rigidbody2DComponent>), typeof and nameof all count. A name counts when its symbol,
    // a containing type or (for types) a base type carries [EngineFeature]. First use per feature, as "path:line: Symbol".
    internal static Dictionary<EngineFeature, string> FeatureUses(CSharpCompilation compilation, SourceFile[] files)
    {
        var found = new Dictionary<EngineFeature, string>();
        string attribute = typeof(EngineFeatureAttribute).FullName!;
        EngineFeature? Marked(ISymbol symbol)
        {
            foreach (var data in symbol.GetAttributes())
                if (data.AttributeClass?.ToDisplayString() == attribute && data.ConstructorArguments is [{ Value: int value }])
                    return (EngineFeature)value;
            return null;
        }
        EngineFeature? Feature(ISymbol symbol)
        {
            symbol = symbol.OriginalDefinition;
            if (symbol is IMethodSymbol { ReducedFrom: { } extension }) symbol = extension;
            if (Marked(symbol) is { } own) return own;
            for (var type = symbol as INamedTypeSymbol ?? symbol.ContainingType; type != null; type = type.ContainingType)
                for (var level = type; level != null; level = level.BaseType)
                    if (Marked(level) is { } feature) return feature;
            return null;
        }
        for (int i = 0; i < files.Length; i++)
        {
            var tree = compilation.SyntaxTrees[i];
            var model = compilation.GetSemanticModel(tree);
            foreach (var name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                var info = model.GetSymbolInfo(name);
                foreach (var symbol in info.Symbol is { } resolved ? [resolved] : info.CandidateSymbols)
                    if (Feature(symbol) is { } feature && !found.ContainsKey(feature))
                        found[feature] = $"{files[i].Path}:{name.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {symbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}";
            }
        }
        return found;
    }
}
