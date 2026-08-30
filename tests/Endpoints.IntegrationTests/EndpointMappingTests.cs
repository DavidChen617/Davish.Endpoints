using System.Net;
using Davish.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Endpoints.IntegrationTests;

public class ApiGroup : IGroupEndpoint
{
    public RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints)
        => endpoints.MapGroup("api");
}

public class PingEndpoint : IEndpoint<ApiGroup>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("ping", () => "pong");
}

// Deliberately not wired to any DI registration below, so these can stand in for a
// group/endpoint whose declared parent/group was never registered.
public class MissingParentGroup : IGroupEndpoint
{
    public RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints)
        => endpoints.MapGroup("missing");
}

public class OrphanChildGroup : IGroupEndpoint<MissingParentGroup>
{
    public RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints)
        => endpoints.MapGroup("orphan");
}

public class OrphanEndpoint : IEndpoint<MissingParentGroup>
{
    public void AddRoutes(IEndpointRouteBuilder endpoints)
        => endpoints.MapGet("orphan-ping", () => "pong");
}

public class EndpointMappingTests
{
    [Fact]
    public async Task MapEndpoints_Registers_Route_Under_Group_Prefix()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddEndpoints();

        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();

        var client = app.GetTestClient();
        var response = await client.GetAsync("/api/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("pong", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MapEndpoints_Throws_With_Clear_Message_When_Parent_Group_Not_Registered()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IGroupEndpoint, OrphanChildGroup>();

        await using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapEndpoints());

        Assert.Contains(nameof(OrphanChildGroup), exception.Message);
        Assert.Contains(nameof(MissingParentGroup), exception.Message);
    }

    [Fact]
    public async Task MapEndpoints_Throws_With_Clear_Message_When_Endpoint_Group_Not_Registered()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IEndpoint, OrphanEndpoint>();

        await using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapEndpoints());

        Assert.Contains(nameof(OrphanEndpoint), exception.Message);
        Assert.Contains(nameof(MissingParentGroup), exception.Message);
    }
}
