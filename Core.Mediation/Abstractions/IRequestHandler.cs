namespace NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

// İki-parametreli handler — değer dönen query/command handler'ları.
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

// Tek-parametreli handler — void command handler'ları. Gövde `Task Handle(...)` yazar.
// DIM (Default Interface Method) ile Task → Task<Unit> köprülenir; dispatcher daima
// iki-parametreli IRequestHandler<TRequest, Unit>'i resolve edip çağırır, DIM araya girer.
// ÖN KOŞUL: void handler class'ları iki-parametreli Handle'ı KENDİSİ implement ETMEMELİ.
public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest<Unit>
{
    new Task Handle(TRequest request, CancellationToken cancellationToken);

    async Task<Unit> IRequestHandler<TRequest, Unit>.Handle(TRequest request, CancellationToken cancellationToken)
    {
        await Handle(request, cancellationToken);
        return Unit.Value;
    }
}
