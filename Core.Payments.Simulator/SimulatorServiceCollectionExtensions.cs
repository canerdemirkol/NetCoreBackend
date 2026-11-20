using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCoreBackend.NArchitecture.Core.Payments.Logging;

namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

public static class SimulatorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the simulated provider (as an <see cref="IPaymentProvider"/>), its in-process gateway behind a logged HTTP client, the
    /// control and the notification sender. Refused in Production: a simulated payment there would be a free subscription (K6). Needs an
    /// <see cref="IPaymentLogWriter"/>.
    /// </summary>
    public static IServiceCollection AddPaymentSimulator(this IServiceCollection services, IHostEnvironment environment, Action<SimulatorOptions> configure)
    {
        if (environment.IsProduction())
            throw new InvalidOperationException("The payment simulator cannot be registered in Production.");

        var options = new SimulatorOptions();
        configure(options);
        services.AddSingleton(options);
        services.AddSingleton<SimulatorBank>();
        services.AddSingleton<ISimulatorNotificationSender, HttpSimulatorNotificationSender>();
        services.AddSingleton<ISimulatorControl, SimulatorControl>();
        services.AddHttpClient(HttpSimulatorNotificationSender.ClientName);
        services.AddHttpClient<SimulatorPaymentProvider>(client => client.BaseAddress = new Uri(SimulatorGateway.BaseAddress))
            .ConfigurePrimaryHttpMessageHandler(sp => new SimulatorGateway(sp.GetRequiredService<SimulatorBank>()))
            .AddPaymentLogging();
        services.AddTransient<IPaymentProvider>(sp => sp.GetRequiredService<SimulatorPaymentProvider>());
        return services;
    }
}
