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

    private static string RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        var references = trustedAssemblies
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(IEndpoint).Assembly.Location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new EndpointGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        var result = driver.GetRunResult();
        var generatedFile = result.Results[0].GeneratedSources
            .First(s => s.HintName == "EndpointRegistration.g.cs");

        return generatedFile.SourceText.ToString();
    }
}
