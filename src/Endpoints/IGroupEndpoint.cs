namespace Davish.Endpoints;

public interface IGroupEndpoint
{
    RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints);
}
