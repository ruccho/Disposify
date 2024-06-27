using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Disposify.Generators;

[Generator]
public class DisposifierGenerator : IIncrementalGenerator
{
    private const string DefaultDisposifierExtensionCode =
/* lang=c#  */"""
              namespace Disposify
              {
                  internal static partial class DisposifyInternal
                  {
                  }
              }
              """;

    #region IIncrementalGenerator Members

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
            "DefaultDisposifierExtension.g.cs",
            SourceText.From(DefaultDisposifierExtensionCode, Encoding.UTF8)));

        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                (s, _) =>
                {
                    if (s is not InvocationExpressionSyntax node) return false;
                    if (node.ArgumentList.Arguments.Count != 0) return false;
                    return true;
                },
                GetDisposifiedType)
            .Where(t => t is { TypeKind: TypeKind.Class } and not INamedTypeSymbol { IsGenericType: true });

        context.RegisterSourceOutput(
            provider.Collect().Select((types, ct) =>
                types.Distinct<ITypeSymbol>(SymbolEqualityComparer.Default).ToArray()),
            Emit);

        var attributeProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Disposify.GenerateDisposifierAttribute",
            (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
            (syntaxContext, _) => syntaxContext
        );
        context.RegisterSourceOutput(attributeProvider, Emit);
    }

    #endregion

    private void Emit(SourceProductionContext sourceContext, GeneratorAttributeSyntaxContext attributeContext)
    {
        if (attributeContext.TargetSymbol is not INamedTypeSymbol typeSymbol) return;

        StringBuilder sourceBuilder = new();

        sourceBuilder.AppendLine("#nullable enable");

        var ns = typeSymbol.ContainingNamespace;

        if (!ns.IsGlobalNamespace)
        {
            sourceBuilder.AppendLine($"namespace {ns}");
            sourceBuilder.AppendLine("{");
        }


        sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                    {{(typeSymbol.IsStatic ? "static " : "")}}partial {{(typeSymbol.TypeKind == TypeKind.Class ? "class" : "struct")}} {{typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat.WithMemberOptions(SymbolDisplayMemberOptions.None))}}
                    {
                """);

        foreach (var attributeData in attributeContext.Attributes)
        {
            if (attributeData.ConstructorArguments.Length != 1) continue;
            if (attributeData.ConstructorArguments[0].Value is not ITypeSymbol targetTypeSymbol) continue;
            EmitDisposifierBody(sourceBuilder, targetTypeSymbol, typeSymbol.IsStatic, true);
        }

        sourceBuilder.AppendLine(
/* lang=c#  */"""    }""");

        if (!ns.IsGlobalNamespace) sourceBuilder.AppendLine($"}} // namespace {ns}");

        var fileNameIdentifier = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Substring("global::".Length)
            .Replace('<', '_')
            .Replace('>', '_');

        sourceContext.AddSource($"{fileNameIdentifier}.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    private void Emit(SourceProductionContext sourceContext, ITypeSymbol?[] types)
    {
        StringBuilder sourceBuilder = new();

        sourceBuilder.AppendLine("#nullable enable");

        sourceBuilder.AppendLine(
/* lang=c#  */"""
              namespace Disposify
              {
                  partial class DisposifyInternal
                  {
              """);
        foreach (var type in types)
        {
            // TODO: generics
            var fqName = type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var disposifierName =
                $"{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted))}Disposifier";
            sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                        public static global::Disposify.Generated.{{disposifierName}} Disposify(this {{fqName}}? target) => new global::Disposify.Generated.{{disposifierName}}(target);
                """);
        }

        sourceBuilder.AppendLine(
/* lang=c#  */"""
                  }
              }
              """);


        sourceBuilder.AppendLine(
/* lang=c#  */"""
              namespace Disposify.Generated
              {
              """);
        foreach (var type in types)
        {
            // TODO: generics
            if (!type!.ContainingNamespace.IsGlobalNamespace)
                sourceBuilder.AppendLine(
                    /* lang=c#  */
                    $$"""
                          namespace {{type.ContainingNamespace}}
                          {
                      """);

            sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                        internal readonly struct {{type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}}Disposifier
                        {
                """);

            EmitDisposifierBody(sourceBuilder, type, false, false);

            sourceBuilder.AppendLine(
/* lang=c#  */"""        }""");
            if (!type.ContainingNamespace.IsGlobalNamespace)
                sourceBuilder.AppendLine(
                    /* lang=c#  */
                    $$"""
                          } // namespace {{type.ContainingNamespace}}
                      """);
        }

        sourceBuilder.AppendLine(
/* lang=c#  */"""} // namespace Disposify.Generated""");

        sourceContext.AddSource("Disposifiers.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    private static void EmitDisposifierBody(StringBuilder sourceBuilder, ITypeSymbol type, bool isStatic,
        bool emitStaticAsStatic)
    {
        var fqName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!isStatic)
            sourceBuilder.AppendLine(
                /* lang=c#  */
                $$"""
                              private readonly {{fqName}}? Target;
                              public {{type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}}Disposifier({{fqName}}? target) => Target = target;

                  """);

        foreach (var eventSymbol in type.GetMembers().OfType<IEventSymbol>())
        {
            if (isStatic && !eventSymbol.IsStatic) continue;
            var isEventStatic = eventSymbol.IsStatic;
            sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                            public {{(isEventStatic && emitStaticAsStatic ? "static " : "")}}global::Disposify.Disposable {{eventSymbol.Name}}({{eventSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}} @delegate)
                            {
                                {{(isEventStatic ? $"{fqName}.{eventSymbol.Name}" : $"Target!.{eventSymbol.Name}")}} += @delegate;
                                return global::Disposify.Disposable.Create({{(isEventStatic ? "(object?)null" : "Target")}}, @delegate, static ({{(isEventStatic ? "_" : "target")}}, @delegate) => {{(isEventStatic ? $"{fqName}.{eventSymbol.Name}" : $"target.{eventSymbol.Name}")}} -= @delegate);
                            }
                """);
        }
    }


    private static ITypeSymbol? GetDisposifiedType(
        GeneratorSyntaxContext context, CancellationToken ct)
    {
        var invocationExpressionSyntax = (InvocationExpressionSyntax)context.Node;

        var untypedOperation = context.SemanticModel.GetOperation(invocationExpressionSyntax,
            ct);
        if (untypedOperation is not IInvocationOperation operation) return null;


        var method = operation.TargetMethod;
        if (method.Name != "Disposify") return null;
        if (method.ContainingType is not
            {
                Name: "DisposifyExtensions", ContainingNamespace: { IsGlobalNamespace: false, Name: "Disposify" },
                ContainingType: null
            }) return null;

        return method.IsStatic ? method.TypeArguments[0] : operation.Arguments[0].Value.Type;
    }
}