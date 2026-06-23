namespace NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

// Pipeline'da bir sonraki adıma geçiş delegesi. Behavior'lar her zaman next(ct) çağırıyor;
// `= default` MediatR 14'ün gerçek imzasıyla uyum için (zorunlu değil, zararsız).
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);
