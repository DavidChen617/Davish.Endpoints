using System.Collections.Immutable;
using Davish.Endpoints;
using Endpoints.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Endpoints.UnitTests;

public class EndpointGeneratorTests
{
    [Fact]
    public void Generates_AddEndpoints_For_Class_Implementing_IEndpoint()
    {
        const string source = """
            using Davish.Endpoints;
            using Microsoft.AspNetCore.Routing;

            namespace TestApp;

            public class LoginEndpoint : IEndpoint
            {
                public void AddRoutes(IEndpointRouteBuilder endpoints) { }
            }
            """;

        string generated = RunGenerator(source);

        Assert.Contains(
            "services.AddSingleton<global::Davish.Endpoints.IEndpoint, global::TestApp.LoginEndpoint>();",
            generated);
    }

    [Fact]
    public void Generates_AddEndpoints_For_Class_Implementing_IGroupEndpoint()
    {
        const string source = """
            using Davish.Endpoints;
            using Microsoft.AspNetCore.Routing;

            namespace TestApp;

            public class ApiGroup : IGroupEndpoint
            {
                public RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints)
                    => endpoints.MapGroup("api");
            }
            """;

        string generated = RunGenerator(source);

        Assert.Contains(
            "services.AddSingleton<global::Davish.Endpoints.IGroupEndpoint, global::TestApp.ApiGroup>();",
            generated);
    }

    [Fact]
    public void Does_Not_Register_Abstract_Class()
    {
        const string source = """
            using Davish.Endpoints;
            using Microsoft.AspNetCore.Routing;

            namespace TestApp;

            public abstract class BaseEndpoint : IEndpoint
            {
                public void AddRoutes(IEndpointRouteBuilder endpoints) { }
            }
            """;

        string generated = RunGenerator(source);

        Assert.DoesNotContain("BaseEndpoint", generated);
    }

    [Fact]
    public void Reports_Diagnostic_When_Endpoints_Group_Is_Not_In_This_Compilation()
    {
        var libraryReference = CompileToReference("""
            using Davish.Endpoints;
            using Microsoft.AspNetCore.Routing;

            namespace Library;

            public class LibraryGroup : IGroupEndpoint
            {
                public RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints)
                    => throw new System.NotImplementedException();
            }
            """);

        const string source = """
            using Davish.Endpoints;
            using Library;
            using Microsoft.AspNetCore.Routing;

            namespace TestApp;

            public class LoginEndpoint : IEndpoint<LibraryGroup>
            {
                public void AddRoutes(IEndpointRouteBuilder endpoints) { }
            }
            """;

        var diagnostics = RunGeneratorDiagnostics(source, [libraryReference]);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("ENDPT001", diagnostic.Id);
        Assert.Contains("LoginEndpoint", diagnostic.GetMessage());
        Assert.Contains("LibraryGroup", diagnostic.GetMessage());
    }

    [Fact]
    public void Reports_Diagnostic_When_Parent_Group_Is_Not_In_This_Compilation()
    {
        var libraryReference = CompileToReference("""
            using Davish.Endpoints;
            using Microsoft.AspNetCore.Routing;

            namespace Library;

            public class LibraryGroup : IGroupEndpoint
            {
                public RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints)
                    => throw new System.NotImplementedException();
            }
            """);

        const string source = """
            using Davish.Endpoints;
            using Library;
            using Microsoft.AspNetCore.Routing;

            namespace TestApp;

            public class AuthGroup : IGroupEndpoint<LibraryGroup>
            {
                public RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints)
                    => endpoints.MapGroup("auth");
            }
            """;

        var diagnostics = RunGeneratorDiagnostics(source, [libraryReference]);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("ENDPT002", diagnostic.Id);
        Assert.Contains("AuthGroup", diagnostic.GetMessage());
        Assert.Contains("LibraryGroup", diagnostic.GetMessage());
    }

    [Fact]
    public void Does_Not_Report_Diagnostic_When_Group_Is_In_This_Compilation()
    {
        const string source = """
            using Davish.Endpoints;
            using Microsoft.AspNetCore.Routing;

            namespace TestApp;

            public class ApiGroup : IGroupEndpoint
            {
                public RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints)
                    => endpoints.MapGroup("api");
            }

            public class PingEndpoint : IEndpoint<ApiGroup>
            {
                public void AddRoutes(IEndpointRouteBuilder endpoints) { }
            }
            """;

        var diagnostics = RunGeneratorDiagnostics(source, []);

        Assert.Empty(diagnostics);
    }

    private static string RunGenerator(string source)
    {
        RunGeneratorDiagnostics(source, [], out var generated);
        return generated;
    }

    private static ImmutableArray<Diagnostic> RunGeneratorDiagnostics(
        string source, IEnumerable<MetadataReference> extraReferences)
        => RunGeneratorDiagnostics(source, extraReferences, out _);

    private static ImmutableArray<Diagnostic> RunGeneratorDiagnostics(
        string source, IEnumerable<MetadataReference> extraReferences, out string generated)
    {
        var compilation = CreateCompilation("TestAssembly", source, extraReferences);

        var driver = CSharpGeneratorDriver.Create(new EndpointGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        var result = driver.GetRunResult();
        var generatedFile = result.Results[0].GeneratedSources
            .First(s => s.HintName == "EndpointRegistration.g.cs");

        generated = generatedFile.SourceText.ToString();
        return result.Results[0].Diagnostics;
    }

    private static MetadataReference CompileToReference(string source)
    {
        var compilation = CreateCompilation("LibraryAssembly", source, []);

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);

        if (!emitResult.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, emitResult.Diagnostics));

        stream.Seek(0, SeekOrigin.Begin);
        return MetadataReference.CreateFromStream(stream);
    }

    private static CSharpCompilation CreateCompilation(
        string assemblyName, string source, IEnumerable<MetadataReference> extraReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        var references = trustedAssemblies
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(IEndpoint).Assembly.Location))
            .Concat(extraReferences)
            .ToList();

        return CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
