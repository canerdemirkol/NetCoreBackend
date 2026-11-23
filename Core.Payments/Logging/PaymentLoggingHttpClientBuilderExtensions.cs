using Microsoft.Extensions.DependencyInjection;

namespace NetCoreBackend.NArchitecture.Core.Payments.Logging;

public static class PaymentLoggingHttpClientBuilderExtensions
{
    /// <summary>Logs every call of this provider client (<see cref="PaymentLoggingHandler"/>); needs an <see cref="IPaymentLogWriter"/>.</summary>
    public static IHttpClientBuilder AddPaymentLogging(this IHttpClientBuilder builder)
    {
        builder.Services.AddTransient<PaymentLoggingHandler>(sp =>
            new PaymentLoggingHandler(sp.GetRequiredService<IPaymentLogWriter>(), sp.GetService<TimeProvider>()));
        return builder.AddHttpMessageHandler<PaymentLoggingHandler>();
    }
}
