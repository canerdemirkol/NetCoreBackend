using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Payments.Logging;

namespace NetCoreBackend.NArchitecture.Core.Payments.PayTr;

public static class PayTrServiceCollectionExtensions
{
    /// <summary>
    /// Registers PayTR as an <see cref="IPaymentProvider"/> behind a logged HTTP client (every call is written to the payment log, masked).
    /// Needs an <see cref="IPaymentLogWriter"/>. <paramref name="configure"/> overrides the documented addresses and paths (<see cref="PayTrOptions"/>).
    /// </summary>
    public static IServiceCollection AddPayTrPayments(this IServiceCollection services, Action<PayTrOptions>? configure = null)
    {
        var options = new PayTrOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddHttpClient<PayTrPaymentProvider>().AddPaymentLogging();
        services.AddTransient<IPaymentProvider>(sp => sp.GetRequiredService<PayTrPaymentProvider>());
        return services;
    }
}
