using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NetCoreBackend.NArchitecture.Core.Payments.Masking;

namespace NetCoreBackend.NArchitecture.Core.Payments.Logging;

/// <summary>
/// Logs every HTTP call a provider makes, so provider code cannot forget it: a row before the request is sent (a failed write stops the
/// send), a second row with the response, or with the failure (timeout, DNS, TLS), which is then thrown on. Headers: Content-Type and
/// User-Agent by value, the provider's signature headers by presence only; cookies and authorization never. Bodies are masked.
/// </summary>
public sealed class PaymentLoggingHandler : DelegatingHandler
{
    private const string Present = "present";
    private static readonly string[] HeadersByValue = ["Content-Type", "User-Agent"];

    private readonly IPaymentLogWriter _writer;
    private readonly TimeProvider _timeProvider;

    public PaymentLoggingHandler(IPaymentLogWriter writer, TimeProvider? timeProvider = null)
    {
        _writer = writer;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        PaymentLogContext context = PaymentLogScope.Current
            ?? throw new InvalidOperationException("A payment provider call outside a PaymentLogScope; it would not be logged.");

        string? requestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        MaskedPayload maskedRequest = PaymentDataMasker.Mask(requestBody, request.Content?.Headers.ContentType?.ToString());
        await _writer.WriteAsync(Entry(context, context.Step, request, PaymentLogOutcome.Succeeded) with
        {
            Headers = HeadersOf(request, context),
            Body = maskedRequest.Body,
            PersonalData = maskedRequest.PersonalData,
        }, cancellationToken);

        PaymentLogStep responseStep = context.ResponseStep ?? context.Step;
        long started = Stopwatch.GetTimestamp();
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            await _writer.WriteAsync(Entry(context, responseStep, request, PaymentLogOutcome.Failed) with
            {
                DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                ExceptionType = ex.GetType().Name,
                ExceptionMessage = ex.Message,
            }, CancellationToken.None);
            throw;
        }

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        MaskedPayload maskedResponse = PaymentDataMasker.Mask(responseBody, response.Content.Headers.ContentType?.ToString());
        await _writer.WriteAsync(Entry(context, responseStep, request, response.IsSuccessStatusCode ? PaymentLogOutcome.Succeeded : PaymentLogOutcome.Failed) with
        {
            HttpStatus = (int)response.StatusCode,
            DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            Body = maskedResponse.Body,
            PersonalData = maskedResponse.PersonalData,
        }, cancellationToken);
        return response;
    }

    private PaymentLogEntry Entry(PaymentLogContext context, PaymentLogStep step, HttpRequestMessage request, PaymentLogOutcome outcome) => new()
    {
        OrderId = context.OrderId,
        AttemptNo = context.AttemptNo,
        CorrelationId = context.CorrelationId,
        IdempotencyKey = context.IdempotencyKey,
        Step = step,
        Direction = PaymentLogDirection.Outgoing,
        Outcome = outcome,
        PayerTenantId = context.PayerTenantId,
        OwnerTenantId = context.OwnerTenantId,
        UserId = context.UserId,
        ImpersonatorUserId = context.ImpersonatorUserId,
        ClientIp = context.ClientIp,
        Provider = context.Provider,
        Environment = context.Environment,
        TestFlag = context.Environment is PaymentEnvironment env ? env == PaymentEnvironment.Test : null,
        BaseAddress = request.RequestUri?.GetLeftPart(UriPartial.Authority),
        MerchantAccountId = context.MerchantAccountId,
        CredentialVersion = context.CredentialVersion,
        CredentialFingerprint = context.CredentialFingerprint,
        Endpoint = request.RequestUri?.AbsolutePath,
        Method = request.Method.Method,
        CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
    };

    private static Dictionary<string, string> HeadersOf(HttpRequestMessage request, PaymentLogContext context)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in HeadersByValue)
            if (TryGetHeader(request, name, out string? value))
                headers[name] = value!;
        foreach (string name in context.SignatureHeaderNames)
            if (TryGetHeader(request, name, out _))
                headers[name] = Present;
        return headers;
    }

    private static bool TryGetHeader(HttpRequestMessage request, string name, out string? value)
    {
        if (request.Headers.TryGetValues(name, out IEnumerable<string>? values)
            || (request.Content?.Headers.TryGetValues(name, out values) ?? false))
        {
            value = string.Join(",", values!);
            return true;
        }
        value = null;
        return false;
    }
}

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
