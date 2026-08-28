using System.Net;
using Davish.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;

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
}
