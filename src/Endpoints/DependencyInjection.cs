namespace Davish.Endpoints;

/// <summary>
/// Provides <see cref="WebApplication"/> extensions for mapping the endpoints and groups registered
/// by the generated <c>AddEndpoints()</c>.
/// </summary>
public static class DependencyInjection
{
    extension(WebApplication app)
    {
        /// <summary>
        /// Resolves every registered <see cref="IGroupEndpoint"/> and <see cref="IEndpoint"/> and maps
        /// them onto <paramref name="app"/>, configuring parent groups before their children and before
        /// the endpoints that belong to them.
        /// </summary>
        public void MapEndpoints()
        {
            var groups = app.Services.GetServices<IGroupEndpoint>().ToList();
            var endpoints = app.Services.GetServices<IEndpoint>().ToList();

            var builders = new Dictionary<Type, RouteGroupBuilder>();

            void ConfigureGroup(IGroupEndpoint group)
            {
                var groupType = group.GetType();

                if (builders.ContainsKey(groupType))
                    return;

                var parentInterfaces = groupType.GetInterfaces()
                    .FirstOrDefault(i =>
                        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IGroupEndpoint<>));

                IEndpointRouteBuilder parentBuilder = app;

                if (parentInterfaces is not null)
                {
                    var parentType = parentInterfaces.GetGenericArguments()[0];
                    var parentGroup = groups.FirstOrDefault(g => g.GetType() == parentType);

                    if (parentGroup is null)
                        throw new InvalidOperationException(
                            $"Endpoint group '{groupType.Name}' declares a parent group '{parentType.Name}' " +
                            $"via IGroupEndpoint<{parentType.Name}>, but no instance of '{parentType.Name}' " +
                            "was registered. Make sure it is registered as an IGroupEndpoint (e.g. via AddEndpoints()).");

                    ConfigureGroup(parentGroup);
                    parentBuilder = builders[parentType];
                }

                builders[groupType] = group.Configure(parentBuilder);
            }

            foreach (var group in groups)
                ConfigureGroup(group);

            foreach (var endpoint in endpoints)
            {
                var groupInterface = endpoint.GetType().GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEndpoint<>));

                IEndpointRouteBuilder builder = app;

                if (groupInterface is not null)
                {
                    var groupType = groupInterface.GetGenericArguments()[0];

                    if (!builders.TryGetValue(groupType, out var groupBuilder))
                        throw new InvalidOperationException(
                            $"Endpoint '{endpoint.GetType().Name}' declares group '{groupType.Name}' " +
                            $"via IEndpoint<{groupType.Name}>, but no instance of '{groupType.Name}' was registered. " +
                            "Make sure it is registered as an IGroupEndpoint (e.g. via AddEndpoints()).");

                    builder = groupBuilder;
                }

                endpoint.AddRoutes(builder);
            }
        }
    }
}
