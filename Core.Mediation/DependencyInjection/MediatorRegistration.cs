using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

namespace NetCoreBackend.NArchitecture.Core.Mediation.DependencyInjection;

// MediatR'ın AddMediatR(...) çağrısının yerine geçen kayıt yardımcısı.
public static class MediatorRegistration
{
    public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        // Mediator stateless; IMediator olarak scoped kaydedilir.
        services.AddScoped<IMediator, Mediator>();

        // Sadece iki-parametreli IRequestHandler<,> taranır. Void handler'lar da bu arayüzü
        // IRequestHandler<TRequest, Unit> olarak (kalıtımla) implement ettiği için yakalanır.
        // Nested class handler'lar (LogoutCommand.LogoutCommandHandler) GetTypes() ile gelir.
        Type handlerInterface = typeof(IRequestHandler<,>);

        foreach (Assembly assembly in assemblies)
        {
            IEnumerable<(Type Service, Type Implementation)> handlerTypes = assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                    .Select(i => (Service: i, Implementation: t)));

            foreach ((Type service, Type implementation) in handlerTypes)
                services.AddScoped(service, implementation);
        }

        return services;
    }
}
