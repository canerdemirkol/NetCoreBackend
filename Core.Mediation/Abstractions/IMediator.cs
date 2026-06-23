namespace NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

// Controller'ların inject ettiği giriş noktası (BaseController GetService<IMediator>() yapıyor).
// NOT: MediatR'da ayrı bir ISender vardır; bu projede HİÇBİR yer ISender kullanmıyor,
// bu yüzden YAGNI gereği eklemedik. İleride gerekirse `interface ISender { Send... }` +
// `IMediator : ISender` olarak ayırmak tek satırlık iş.
public interface IMediator
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
