namespace Davish.Endpoints;

/// <summary>
/// An <see cref="IGroupEndpoint"/> nested under the parent group configured by <typeparamref name="TGroup"/>.
/// <c>MapEndpoints()</c> maps <typeparamref name="TGroup"/> before this group.
/// </summary>
/// <typeparam name="TGroup">The parent group this group is nested under.</typeparam>
public interface IGroupEndpoint<TGroup> : IGroupEndpoint where TGroup : IGroupEndpoint;
