namespace NetCoreBackend.NArchitecture.Core.Mediation;

// TResponse'a bağlı ara katman — Send<TResponse>'in cast edebileceği tip.
internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerWrapperBase
{
    public abstract Task<TResponse> Handle(object request, IServiceProvider sp, CancellationToken ct);
}
