namespace Davish.Endpoints;

/// <summary>
/// Implemented by a class that maps one or more routes. Discovered at compile time by the
/// source generator and registered as a singleton via the generated <c>AddEndpoints()</c>.
/// </summary>
public interface IEndpoint
{
    /// <summary>
    /// Maps this endpoint's routes onto <paramref name="endpoints"/>.
    /// </summary>
    /// <param name="endpoints">The route builder to map routes onto.</param>
    public void AddRoutes(IEndpointRouteBuilder endpoints);
}
