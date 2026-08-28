#nullable enable
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Endpoints.SourceGenerator;

[Generator]
public class EndpointGenerator : IIncrementalGenerator
{
    private const string EndpointInterfaceMetadataName = "Davish.Endpoints.IEndpoint";
    private const string GroupInterfaceMetadataName = "Davish.Endpoints.IGroupEndpoint";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. 語法快篩:只挑有 BaseList 的 class
        IncrementalValuesProvider<INamedTypeSymbol?> candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList.Types.Count: > 0 },
                transform: static (ctx, _) =>
                    ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol)
            .Where(static symbol => symbol is not null);

        // 2. 精準取得兩個目標介面
        IncrementalValueProvider<(INamedTypeSymbol? Endpoint, INamedTypeSymbol? Group)> targetInterfaces =
            context.CompilationProvider.Select(static (compilation, _) =>
                (
                    Endpoint: compilation.GetTypeByMetadataName(EndpointInterfaceMetadataName),
                    Group: compilation.GetTypeByMetadataName(GroupInterfaceMetadataName)
                ));

        // 3. 對每個候選類別,分別判斷它是不是 IEndpoint 或 IGroupEndpoint
        IncrementalValuesProvider<(INamedTypeSymbol Symbol, bool IsEndpoint, bool IsGroup)> classified = candidates
            .Combine(targetInterfaces)
            .Select(static (pair, _) =>
            {
                var (symbol, ifaces) = pair;
                bool isEndpoint = ifaces.Endpoint is not null &&
                                   symbol!.AllInterfaces.Contains(ifaces.Endpoint, SymbolEqualityComparer.Default);
                bool isGroup = ifaces.Group is not null &&
                               symbol!.AllInterfaces.Contains(ifaces.Group, SymbolEqualityComparer.Default);
                return (Symbol: symbol!, IsEndpoint: isEndpoint, IsGroup: isGroup);
            })
            .Where(static x => (x.IsEndpoint || x.IsGroup) && !x.Symbol.IsAbstract);

        // 4. 收集全部,一次輸出
        IncrementalValueProvider<ImmutableArray<(INamedTypeSymbol Symbol, bool IsEndpoint, bool IsGroup)>> collected =
            classified.Collect();

        context.RegisterSourceOutput(collected, static (spc, items) =>
        {
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
