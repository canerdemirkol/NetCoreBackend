using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Payments.Logging;

namespace NetCoreBackend.NArchitecture.Core.Payments.Iyzico;

public static class IyzicoServiceCollectionExtensions
{
    /// <summary>
    /// Registers iyzico as an <see cref="IPaymentProvider"/> behind a logged HTTP client (every call is written to the payment log, masked).
    /// Needs an <see cref="IPaymentLogWriter"/>. The address follows each merchant account's environment (sandbox or live).
    /// </summary>
    public static IServiceCollection AddIyzicoPayments(this IServiceCollection services)
    {
        services.AddHttpClient<IyzicoPaymentProvider>().AddPaymentLogging();
        services.AddTransient<IPaymentProvider>(sp => sp.GetRequiredService<IyzicoPaymentProvider>());
        return services;
    }
}
