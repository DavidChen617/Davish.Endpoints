namespace Davish.Endpoints;

/// <summary>
/// Implemented by a class that configures a route group. Discovered at compile time by the
/// source generator and registered as a singleton via the generated <c>AddEndpoints()</c>.
/// </summary>
public interface IGroupEndpoint
{
    /// <summary>
    /// Configures this route group under <paramref name="endpoints"/>.
    /// </summary>
    /// <param name="endpoints">The route builder to map this group onto.</param>
    /// <returns>The configured route group builder, used to map child groups and endpoints.</returns>
    RouteGroupBuilder Configure(IEndpointRouteBuilder endpoints);
}
