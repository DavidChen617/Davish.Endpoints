namespace Davish.Endpoints;

public interface IGroupEndpoint<TGroup> : IGroupEndpoint where TGroup : IGroupEndpoint;
