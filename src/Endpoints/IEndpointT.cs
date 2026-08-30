namespace Davish.Endpoints;

/// <summary>
/// An <see cref="IEndpoint"/> that belongs to the route group configured by <typeparamref name="TGroup"/>.
/// <c>MapEndpoints()</c> maps <typeparamref name="TGroup"/> before this endpoint's routes.
/// </summary>
/// <typeparam name="TGroup">The group this endpoint's routes are mapped under.</typeparam>
public interface IEndpoint<TGroup> : IEndpoint where TGroup : IGroupEndpoint;
