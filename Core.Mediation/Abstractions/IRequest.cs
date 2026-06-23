namespace NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

// "Bu sınıf TResponse dönen bir istektir" — marker, davranışı yok.
public interface IRequest<out TResponse> { }

// Non-generic marker — void command'lar için. (Bizim tasarım: IRequest : IRequest<Unit>.)
public interface IRequest : IRequest<Unit> { }
