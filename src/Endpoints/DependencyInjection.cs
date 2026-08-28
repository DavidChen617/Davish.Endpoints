namespace Davish.Endpoints;

public static class DependencyInjection
{
    extension(WebApplication app)
    {
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
                    var parentGroup = groups.First(g => g.GetType() == parentType);
                    ConfigureGroup(parentGroup);
                    parentBuilder = builders[parentType];
                }

                builders[groupType] = group.Configure(parentBuilder);
            }

            foreach (var group in groups)
                ConfigureGroup(group);

            foreach (var endpoint in endpoints)
            {
                var groupInterface = endpoint.GetType().GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEndpoint<>));
                var groupType = groupInterface.GetGenericArguments()[0];
                endpoint.AddRoutes(builders[groupType]);
            }
        }
    }
}
