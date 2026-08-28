namespace Davish.Endpoints;

public interface IEndpoint<TGroup> : IEndpoint where TGroup : IGroupEndpoint;
