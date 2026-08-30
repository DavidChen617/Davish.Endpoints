#nullable enable
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Endpoints.SourceGenerator;

[Generator]
public class EndpointGenerator : IIncrementalGenerator
{
    private const string EndpointInterfaceMetadataName = "Davish.Endpoints.IEndpoint";
    private const string GroupInterfaceMetadataName = "Davish.Endpoints.IGroupEndpoint";
    private const string EndpointOfTInterfaceMetadataName = "Davish.Endpoints.IEndpoint`1";
    private const string GroupOfTInterfaceMetadataName = "Davish.Endpoints.IGroupEndpoint`1";

    /// <summary>
    /// Reported when an <c>IEndpoint&lt;TGroup&gt;</c> declares a <c>TGroup</c> that is not
    /// itself found among the <c>IGroupEndpoint</c> classes in this compilation. Since
    /// <c>AddEndpoints()</c> is generated per-compilation, that group won't be registered here,
    /// and <c>MapEndpoints()</c> will throw at runtime unless it's registered another way.
    /// </summary>
    private static readonly DiagnosticDescriptor EndpointGroupNotInCompilationRule = new(
        id: "ENDPT001",
        title: "Endpoint's group is not part of this compilation",
        messageFormat:
            "'{0}' declares group '{1}' via IEndpoint<{1}>, but '{1}' was not found among the IGroupEndpoint " +
            "classes in this compilation, so AddEndpoints() will not register it here; MapEndpoints() will " +
            "throw at runtime unless '{1}' is registered another way",
        category: "Davish.Endpoints",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Same as <see cref="EndpointGroupNotInCompilationRule"/>, but for an
    /// <c>IGroupEndpoint&lt;TParent&gt;</c> whose declared parent group is missing.
    /// </summary>
    private static readonly DiagnosticDescriptor ParentGroupNotInCompilationRule = new(
        id: "ENDPT002",
        title: "Parent group is not part of this compilation",
        messageFormat:
            "'{0}' declares a parent group '{1}' via IGroupEndpoint<{1}>, but '{1}' was not found among the " +
            "IGroupEndpoint classes in this compilation, so AddEndpoints() will not register it here; " +
            "MapEndpoints() will throw at runtime unless '{1}' is registered another way",
        category: "Davish.Endpoints",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Fast syntax filter: only classes with a base list
        IncrementalValuesProvider<INamedTypeSymbol?> candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList.Types.Count: > 0 },
                transform: static (ctx, _) =>
                    ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol)
            .Where(static symbol => symbol is not null);

        // 2. Precisely resolve the four target interfaces (including the open generics IEndpoint<> / IGroupEndpoint<>)
        IncrementalValueProvider<(INamedTypeSymbol? Endpoint, INamedTypeSymbol? Group, INamedTypeSymbol? EndpointOfT, INamedTypeSymbol? GroupOfT)> targetInterfaces =
            context.CompilationProvider.Select(static (compilation, _) =>
                (
                    Endpoint: compilation.GetTypeByMetadataName(EndpointInterfaceMetadataName),
                    Group: compilation.GetTypeByMetadataName(GroupInterfaceMetadataName),
                    EndpointOfT: compilation.GetTypeByMetadataName(EndpointOfTInterfaceMetadataName),
                    GroupOfT: compilation.GetTypeByMetadataName(GroupOfTInterfaceMetadataName)
                ));

        // 3. For each candidate class, determine whether it's IEndpoint / IGroupEndpoint, and record its declared group / parent group (if any)
        IncrementalValuesProvider<(INamedTypeSymbol Symbol, bool IsEndpoint, bool IsGroup, INamedTypeSymbol? DeclaredGroup, INamedTypeSymbol? DeclaredParentGroup)> classified = candidates
            .Combine(targetInterfaces)
            .Select(static (pair, _) =>
            {
                var (symbol, ifaces) = pair;
                bool isEndpoint = ifaces.Endpoint is not null &&
                                   symbol!.AllInterfaces.Contains(ifaces.Endpoint, SymbolEqualityComparer.Default);
                bool isGroup = ifaces.Group is not null &&
                               symbol!.AllInterfaces.Contains(ifaces.Group, SymbolEqualityComparer.Default);

                INamedTypeSymbol? declaredGroup = ifaces.EndpointOfT is null
                    ? null
                    : symbol!.AllInterfaces
                        .FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, ifaces.EndpointOfT))?
                        .TypeArguments.FirstOrDefault() as INamedTypeSymbol;

                INamedTypeSymbol? declaredParentGroup = ifaces.GroupOfT is null
                    ? null
                    : symbol!.AllInterfaces
                        .FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, ifaces.GroupOfT))?
                        .TypeArguments.FirstOrDefault() as INamedTypeSymbol;

                return (Symbol: symbol!, IsEndpoint: isEndpoint, IsGroup: isGroup,
                    DeclaredGroup: declaredGroup, DeclaredParentGroup: declaredParentGroup);
            })
            .Where(static x => (x.IsEndpoint || x.IsGroup) && !x.Symbol.IsAbstract);

        // 4. Collect everything and emit once
        IncrementalValueProvider<ImmutableArray<(INamedTypeSymbol Symbol, bool IsEndpoint, bool IsGroup, INamedTypeSymbol? DeclaredGroup, INamedTypeSymbol? DeclaredParentGroup)>> collected =
            classified.Collect();

        context.RegisterSourceOutput(collected, static (spc, items) =>
        {
            var groupSymbols = items
                .Where(x => x.IsGroup)
                .Select(x => x.Symbol)
                .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var item in items)
            {
                if (item.DeclaredGroup is not null && !groupSymbols.Contains(item.DeclaredGroup))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        EndpointGroupNotInCompilationRule,
                        item.Symbol.Locations.FirstOrDefault(),
                        item.Symbol.Name,
                        item.DeclaredGroup.Name));
                }

                if (item.DeclaredParentGroup is not null && !groupSymbols.Contains(item.DeclaredParentGroup))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        ParentGroupNotInCompilationRule,
                        item.Symbol.Locations.FirstOrDefault(),
                        item.Symbol.Name,
                        item.DeclaredParentGroup.Name));
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine();
            sb.AppendLine("namespace Davish.Endpoints;");
            sb.AppendLine();
            sb.AppendLine("public static class EndpointServiceCollectionExtensions");
            sb.AppendLine("{");
            sb.AppendLine("    public static IServiceCollection AddEndpoints(this IServiceCollection services)");
            sb.AppendLine("    {");

            foreach (var item in items)
            {
                string fullName = item.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                if (item.IsEndpoint)
                    sb.AppendLine($"        services.AddSingleton<global::{EndpointInterfaceMetadataName}, {fullName}>();");

                if (item.IsGroup)
                    sb.AppendLine($"        services.AddSingleton<global::{GroupInterfaceMetadataName}, {fullName}>();");
            }

            sb.AppendLine();
            sb.AppendLine("        return services;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("EndpointRegistration.g.cs", sb.ToString());
        });
    }
}
