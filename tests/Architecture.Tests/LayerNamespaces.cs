using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Architecture.Tests;

internal static class LayerNamespaces
{
    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Presentation", "PublicApi"];

    public static string[] Check(string root, string path, string source)
    {
        string[] parts = path.Replace('\\', '/').Split('/');
        string? folderLayer = parts.Length > 1 ? Layers.FirstOrDefault(layer => layer == parts[0]) : null;
        List<string> errors = [];
        SyntaxNode syntax = CSharpSyntaxTree.ParseText(source).GetRoot();
        foreach (SyntaxNode declaration in syntax.DescendantNodes().Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax))
        {
            string name = string.Join('.', declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(item => item.Name.ToString()));
            string? namespaceLayer = Layers.FirstOrDefault(layer => name == root + "." + layer || name.StartsWith(root + "." + layer + ".", StringComparison.Ordinal));
            if ((folderLayer is not null && namespaceLayer != folderLayer) || (namespaceLayer is not null && namespaceLayer != folderLayer))
                errors.Add($"{path}: namespace {name} does not match layer folder {folderLayer ?? "<root>"}");
        }
        return errors.ToArray();
    }
}
