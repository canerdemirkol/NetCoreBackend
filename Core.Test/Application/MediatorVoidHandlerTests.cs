using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;
using NetCoreBackend.NArchitecture.Core.Mediation.DependencyInjection;

namespace NetCoreBackend.NArchitecture.Core.Test.Application;

// DIM köprüsü doğrulama spike'ı: tek-parametreli void handler (IRequestHandler<TRequest>,
// gövde Task Handle) DI'dan IRequestHandler<TRequest, Unit> olarak resolve edilip DIM köprüsü
// üzerinden çağrılabiliyor mu? Çalışmazsa tüm void command'lar sessizce kırılır.
public sealed class MediatorVoidHandlerTests
{
    [Fact]
    public async Task Void_Handler_Resolves_And_Runs_Through_DIM_Bridge()
    {
        var probe = new List<string>();

        var services = new ServiceCollection();
        services.AddMediator(typeof(MediatorVoidHandlerTests).Assembly);
        services.AddSingleton(probe);
        using var sp = services.BuildServiceProvider();

        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(new PingCommand());

        Assert.Equal(new[] { "handled" }, probe);
    }

    public sealed class PingCommand : IRequest { }

    public sealed class PingHandler : IRequestHandler<PingCommand>
    {
        private readonly List<string> _probe;
        public PingHandler(List<string> probe) => _probe = probe;
        public Task Handle(PingCommand request, CancellationToken ct)
        {
            _probe.Add("handled");
            return Task.CompletedTask;
        }
    }
}
