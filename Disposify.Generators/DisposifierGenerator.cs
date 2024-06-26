using System.Collections.Immutable;
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
    private static SymbolDisplayFormat FullyQualifiedNoGlobalNamespace =
        SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

    private const string DefaultDisposifierExtensionCode =
/* lang=c#  */$$"""
                namespace Disposify
                {
                    internal static partial class DisposifyInternal
                    {
                    }
                }
                """;

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
    }

    private void Emit(SourceProductionContext sourceContext, ITypeSymbol?[] types)
    {
        StringBuilder sourceBuilder = new();

        sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                namespace Disposify
                {
                    partial class DisposifyInternal
                    {
                """);
        foreach (var type in types)
        {
            // TODO: generics
            var fqName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var disposifierName = $"{type.ToDisplayString(FullyQualifiedNoGlobalNamespace)}Disposifier";
            sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                        public static global::Disposify.Generated.{{disposifierName}} Disposify(this {{fqName}}? target) => new global::Disposify.Generated.{{disposifierName}}(target);
                """);
        }

        sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                    }
                }
                """);


        sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                namespace Disposify.Generated
                {
                """);
        foreach (var type in types)
        {
            // TODO: generics
            var fqName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (!type.ContainingNamespace.IsGlobalNamespace)
            {
                sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                    namespace {{type.ContainingNamespace}}
                    {
                """);
            }

            sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                        internal readonly struct {{type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}}Disposifier
                        {
                            private readonly {{fqName}} Target;
                            public {{type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}}Disposifier({{fqName}} target) => Target = target;
                            
                """);

            foreach (var eventSymbol in type.GetMembers().OfType<IEventSymbol>())
            {
                var lhs = eventSymbol.IsStatic ? $"{fqName}.{eventSymbol.Name}" : $"Target.{eventSymbol.Name}";
                sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                            public global::Disposify.Disposable {{eventSymbol.Name}}({{eventSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}} @delegate)
                            {
                                {{lhs}} += @delegate;
                                return global::Disposify.Disposable.Create(Target, @delegate, static (Target, @delegate) => {{lhs}} -= @delegate);
                            }
                """);
            }

            sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                        }
                """);
            if (!type.ContainingNamespace.IsGlobalNamespace)
            {
                sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                    } // namespace {{type.ContainingNamespace}}
                """);
            }
        }

        sourceBuilder.AppendLine(
/* lang=c#  */$$"""
                } // namespace Disposify.Generated
                """);

        sourceContext.AddSource($"Disposifiers.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
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