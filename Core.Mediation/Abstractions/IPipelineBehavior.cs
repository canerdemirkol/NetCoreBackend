namespace NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

// Pipeline behavior contract'ı.
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
