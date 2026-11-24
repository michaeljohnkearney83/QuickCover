using Mediator;
using Microsoft.Extensions.DependencyInjection;
using QuickCover.Application.Common;

namespace QuickCover.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoped (not the default Singleton) because handlers depend on scoped services like the DbContext.
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services;
    }
}
