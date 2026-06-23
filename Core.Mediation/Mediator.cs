using System.Collections.Concurrent;
using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

namespace NetCoreBackend.NArchitecture.Core.Mediation;

// Runtime dispatcher.
// LIFETIME SÖZLEŞMESİ: Mediator yalnızca Scoped (veya Transient) kaydedilmeli — ASLA Singleton.
// Çünkü ctor'da inject edilen IServiceProvider, scoped handler'ları (AddScoped) çözmek için
// scope'a bağlı olmalı. Singleton olursa root provider yakalanır ve scoped handler resolution
// "Cannot resolve scoped service from root provider" ile patlar. (_wrapperCache static olduğu
// için cache zaten lifetime'dan bağımsız — Scoped olmak performansı düşürmez.)
public sealed class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    // Request tipi başına tek wrapper instance cache'lenir; MakeGenericType + Activator pahalı.
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapperBase> _wrapperCache = new();

    public Mediator(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Type requestType = request.GetType();
        RequestHandlerWrapperBase wrapper = _wrapperCache.GetOrAdd(requestType, static t =>
        {
            // TResponse'u request tipinden çıkar. Void command'da IRequest (non-generic,
            // IsGenericType=false) atlanır, IRequest<Unit> seçilir → responseType = Unit.
            Type responseType = t.GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                .GetGenericArguments()[0];

            Type wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(t, responseType);
            return (RequestHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
        });

        return ((RequestHandlerWrapper<TResponse>)wrapper).Handle(request, _serviceProvider, cancellationToken);
    }
}
