using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

namespace NetCoreBackend.NArchitecture.Core.Mediation;

// Mediator'ın runtime tip-çözümleme yardımcıları. Send<TResponse> jenerik argümanı runtime'da
// bilinmediği için, request tipine bağlı pipeline kurulumunu bu wrapper hiyerarşisi üstlenir.

// Generic-olmayan base — cache içinde tutmak için.
internal abstract class RequestHandlerWrapperBase { }

// TResponse'a bağlı ara katman — Send<TResponse>'in cast edebileceği tip.
internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerWrapperBase
{
    public abstract Task<TResponse> Handle(object request, IServiceProvider sp, CancellationToken ct);
}

// Tam tipli wrapper — pipeline'ı kurar ve çalıştırır.
internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> Handle(object request, IServiceProvider sp, CancellationToken ct)
    {
        var handler = sp.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        var behaviors = sp.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();

        // İnnermost: gerçek handler çağrısı.
        RequestHandlerDelegate<TResponse> pipeline = cancellation => handler.Handle((TRequest)request, cancellation);

        // İçten dışa sar: kayıt sırasındaki ilk behavior en dışta kalsın (guard'lar önce çalışsın).
        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            IPipelineBehavior<TRequest, TResponse> current = behaviors[i];
            RequestHandlerDelegate<TResponse> next = pipeline;
            pipeline = cancellation => current.Handle((TRequest)request, next, cancellation);
        }

        return pipeline(ct);
    }
}
