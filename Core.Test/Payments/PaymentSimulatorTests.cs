using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NetCoreBackend.NArchitecture.Core.Payments;
using NetCoreBackend.NArchitecture.Core.Payments.Logging;
using NetCoreBackend.NArchitecture.Core.Payments.Simulator;

namespace Core.Test.Payments;

/// <summary>
/// The simulator behaves like the two real providers (a hosted form with a signed return; a server notification answered "OK"), with a
/// magic card ending for each outcome, and its provider calls go through the same logged HTTP path as real ones.
/// </summary>
public sealed class PaymentSimulatorTests : IDisposable
{
    private const string PageBase = "https://app.test/api/payments/simulator/pay";
    private static readonly Money Hundred = Money.Of(100m);

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class RecordingWriter : IPaymentLogWriter
    {
        public List<PaymentLogEntry> Entries { get; } = [];
        public Task WriteAsync(PaymentLogEntry entry, CancellationToken cancellationToken) { Entries.Add(entry); return Task.CompletedTask; }
    }

    private sealed class RecordingSender : ISimulatorNotificationSender
    {
        public List<(string Url, IReadOnlyDictionary<string, string> Fields)> Sent { get; } = [];
        public Task<string> SendAsync(string url, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
        {
            Sent.Add((url, fields));
            return Task.FromResult("OK");
        }
    }

    private readonly ServiceProvider _services;
    private readonly RecordingWriter _log = new();
    private readonly RecordingSender _sender = new();
    private readonly IPaymentProvider _provider;
    private readonly ISimulatorControl _control;
    private readonly IDisposable _scope;

    public PaymentSimulatorTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPaymentLogWriter>(_log);
        services.AddPaymentSimulator(new Environment(Environments.Development), o => o.PageBaseUrl = PageBase);
        services.AddSingleton<ISimulatorNotificationSender>(_sender);
        _services = services.BuildServiceProvider();
        _provider = _services.GetRequiredService<IEnumerable<IPaymentProvider>>().Single(p => p.Name == PaymentProviderName.Simulator);
        _control = _services.GetRequiredService<ISimulatorControl>();
        _scope = PaymentLogScope.Begin(new PaymentLogContext { OrderId = Guid.NewGuid(), Step = PaymentLogStep.StartRequestSent, Provider = PaymentProviderName.Simulator });
    }

    public void Dispose()
    {
        _scope.Dispose();
        _services.Dispose();
    }

    private static MerchantCredentials Merchant(SimulatorFlow flow = SimulatorFlow.HostedForm, string secret = "secret-1") =>
        new("M-1", "api-1", secret, PaymentEnvironment.Test, new Dictionary<string, string> { [SimulatorOptions.FlowKey] = flow.ToString() });

    private async Task<(string OrderNo, string Token)> StartAsync(MerchantCredentials merchant)
    {
        string orderNo = $"ORD-{Guid.NewGuid():N}"[..16];
        PaymentStartResult started = await _provider.StartAsync(new PaymentStartRequest(orderNo, Hundred,
            Buyer,
            "https://app.test/api/payments/callback/simulator", "https://app.test/api/payments/return/simulator", "Pro, aylık"), merchant, CancellationToken.None);
        Assert.True(started.Succeeded, started.ErrorMessage);
        return (orderNo, started.ProviderToken!);
    }

    private static string Card(string last4) => "400000000000" + last4;

    // The payer as an application builds them from its billing details; the simulator shows the full name on its page.
    private static readonly PaymentBuyer Buyer = new("U-1", "Ayşe", "Yılmaz", "ayse@test.local", "+905551112233", "Atatürk Cd. 1", "İstanbul", "Turkey",
        "34000", null, "10.0.0.1");

    [Fact]
    public async Task Start_GivesThePaymentPageAddress_LikeARealProvider()
    {
        PaymentStartResult started = await _provider.StartAsync(new PaymentStartRequest("ORD-PAGE-1", Hundred,
            Buyer,
            "https://app.test/api/payments/callback/simulator", "https://app.test/api/payments/return/simulator", "Pro, aylık"), Merchant(), CancellationToken.None);

        Assert.Equal($"{PageBase}/{started.ProviderToken}", started.PaymentPageUrl);
    }

    [Fact]
    public void ReferenceOf_ReadsTheOrderNumber_BeforeAnyCredentialIsKnown()
    {
        var message = new IncomingPaymentMessage(new Dictionary<string, string> { ["order_no"] = "ORD-REF-1", ["hash"] = "unchecked" }, new Dictionary<string, string>());

        PaymentMessageReference reference = _provider.ReferenceOf(message);

        Assert.Equal(new PaymentMessageReference("ORD-REF-1", null), reference);
        Assert.Equal(new PaymentMessageReference(null, null),
            _provider.ReferenceOf(new IncomingPaymentMessage(new Dictionary<string, string>(), new Dictionary<string, string>())));
    }

    [Fact]
    public async Task TheHostedForm_GivesAPage_AndASignedReturn_ThatVerifies()
    {
        MerchantCredentials merchant = Merchant();
        (string orderNo, string token) = await StartAsync(merchant);

        SimulatorSubmitResult submitted = await _control.SubmitCardAsync(token, Card("0000"), CancellationToken.None);
        NotificationVerification verified = await _provider.VerifyNotificationAsync(new IncomingPaymentMessage(submitted.ReturnFields, new Dictionary<string, string>()), merchant, CancellationToken.None);

        Assert.Contains(token, _control.RenderPaymentPage(token));
        Assert.Equal("https://app.test/api/payments/return/simulator", submitted.ReturnUrl);
        Assert.Contains("<form", submitted.AutoPostHtml);
        Assert.True(verified.SignatureValid);
        Assert.Equal((PaymentResultKind.Paid, orderNo, "M-1"), (verified.Result!.Value, verified.OrderNo, verified.MerchantId));
        Assert.Equal(Hundred, verified.Amount);
        Assert.Equal("0000", verified.CardLast4);
        Assert.NotNull(verified.ProviderPaymentId);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task TheServerNotificationFlow_SendsASignedNotification_AnsweredOk()
    {
        MerchantCredentials merchant = Merchant(SimulatorFlow.ServerNotification);
        (string orderNo, string token) = await StartAsync(merchant);

        await _control.SubmitCardAsync(token, Card("0000"), CancellationToken.None);

        var (url, fields) = Assert.Single(_sender.Sent);
        NotificationVerification verified = await _provider.VerifyNotificationAsync(new IncomingPaymentMessage(fields, new Dictionary<string, string>()), merchant, CancellationToken.None);
        Assert.Equal("https://app.test/api/payments/callback/simulator", url);
        Assert.True(verified.SignatureValid);
        Assert.Equal(orderNo, verified.OrderNo);
        Assert.Equal("OK", verified.ResponseToProvider);
    }

    [Theory]
    [InlineData("0002", PaymentResultKind.Failed, "card_declined", 100)]
    [InlineData("0003", PaymentResultKind.Failed, "3ds_failed", 100)]
    [InlineData("0004", PaymentResultKind.Paid, null, 101)]   // the notification says another amount
    public async Task EachMagicCard_GivesItsOutcome(string last4, PaymentResultKind result, string? error, decimal amount)
    {
        MerchantCredentials merchant = Merchant(SimulatorFlow.ServerNotification);
        (_, string token) = await StartAsync(merchant);

        await _control.SubmitCardAsync(token, Card(last4), CancellationToken.None);

        NotificationVerification verified = await _provider.VerifyNotificationAsync(new IncomingPaymentMessage(_sender.Sent.Single().Fields, new Dictionary<string, string>()), merchant, CancellationToken.None);
        Assert.True(verified.SignatureValid);
        Assert.Equal(result, verified.Result);
        Assert.Equal(error, verified.ErrorCode);
        Assert.Equal(Money.Of(amount), verified.Amount);
    }

    [Fact]
    public async Task ADelayedNotification_WaitsForTheControl_ADoubleOneComesTwice_AndATamperedOneFailsItsSignature()
    {
        MerchantCredentials merchant = Merchant(SimulatorFlow.ServerNotification);
        (string delayed, string delayedToken) = await StartAsync(merchant);
        (_, string doubleToken) = await StartAsync(merchant);
        (_, string tamperedToken) = await StartAsync(merchant);

        await _control.SubmitCardAsync(delayedToken, Card("0005"), CancellationToken.None);
        int afterDelayed = _sender.Sent.Count;
        await _control.SendNotificationAsync(delayed, times: 1, tamper: false, CancellationToken.None);
        int afterRelease = _sender.Sent.Count;
        await _control.SubmitCardAsync(doubleToken, Card("0006"), CancellationToken.None);
        int afterDouble = _sender.Sent.Count;
        await _control.SubmitCardAsync(tamperedToken, Card("0007"), CancellationToken.None);

        Assert.Equal((0, 1, 3), (afterDelayed, afterRelease, afterDouble));
        Assert.False((await _provider.VerifyNotificationAsync(new IncomingPaymentMessage(_sender.Sent[^1].Fields, new Dictionary<string, string>()), merchant, CancellationToken.None)).SignatureValid);
    }

    [Fact]
    public async Task TheStatus_IsPendingUntilPaid_ThenPaid()
    {
        MerchantCredentials merchant = Merchant();
        (string orderNo, string token) = await StartAsync(merchant);

        PaymentStatusResult before = await _provider.QueryAsync(new PaymentReference(orderNo, null, null), merchant, CancellationToken.None);
        await _control.SubmitCardAsync(token, Card("0000"), CancellationToken.None);
        PaymentStatusResult after = await _provider.QueryAsync(new PaymentReference(orderNo, null, null), merchant, CancellationToken.None);

        Assert.Equal(ProviderPaymentState.Pending, before.State);
        Assert.Equal(ProviderPaymentState.Paid, after.State);
        Assert.Equal(Hundred, after.Amount);
    }

    [Fact]
    public async Task ACancel_WorksBeforeTheDayCloses_ARefundAfter_AndRefundsCannotExceedThePayment()
    {
        MerchantCredentials merchant = Merchant();
        (string open, string openToken) = await StartAsync(merchant);
        (string closed, string closedToken) = await StartAsync(merchant);
        await _control.SubmitCardAsync(openToken, Card("0000"), CancellationToken.None);
        await _control.SubmitCardAsync(closedToken, Card("0000"), CancellationToken.None);
        var openRef = new PaymentReference(open, null, null);
        var closedRef = new PaymentReference(closed, null, null);

        PaymentActionResult refundTooEarly = await _provider.RefundAsync(new RefundRequest(openRef, Money.Of(10m)), merchant, CancellationToken.None);
        PaymentActionResult cancelled = await _provider.CancelAsync(openRef, merchant, CancellationToken.None);
        _control.CloseDay();
        PaymentActionResult cancelTooLate = await _provider.CancelAsync(closedRef, merchant, CancellationToken.None);
        PaymentActionResult partial = await _provider.RefundAsync(new RefundRequest(closedRef, Money.Of(60m)), merchant, CancellationToken.None);
        PaymentActionResult tooMuch = await _provider.RefundAsync(new RefundRequest(closedRef, Money.Of(50m)), merchant, CancellationToken.None);
        PaymentActionResult rest = await _provider.RefundAsync(new RefundRequest(closedRef, Money.Of(40m)), merchant, CancellationToken.None);

        Assert.Equal((false, "day_open_use_cancel"), (refundTooEarly.Succeeded, refundTooEarly.ErrorCode));
        Assert.True(cancelled.Succeeded);
        Assert.Equal((false, "day_closed_use_refund"), (cancelTooLate.Succeeded, cancelTooLate.ErrorCode));
        Assert.True(partial.Succeeded);
        Assert.Equal((false, "exceeds_paid_amount"), (tooMuch.Succeeded, tooMuch.ErrorCode));
        Assert.True(rest.Succeeded);
        Assert.Equal(ProviderPaymentState.Refunded, (await _provider.QueryAsync(closedRef, merchant, CancellationToken.None)).State);
    }

    [Fact]
    public async Task ProviderCalls_GoThroughTheLoggedHttpPath()
    {
        await StartAsync(Merchant());

        Assert.Contains(_log.Entries, e => e.Direction == PaymentLogDirection.Outgoing && e.BaseAddress == SimulatorGateway.BaseAddress.TrimEnd('/'));
        Assert.All(_log.Entries, e => Assert.DoesNotContain("secret-1", e.Body ?? ""));
    }

    [Fact]
    public async Task AWrongSecret_IsRefusedByTheGateway()
    {
        (string orderNo, _) = await StartAsync(Merchant());

        PaymentStatusResult refused = await _provider.QueryAsync(new PaymentReference(orderNo, null, null), Merchant(secret: "wrong"), CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Equal("unauthorized", refused.ErrorCode);
    }

    // The simulator lives in memory; a restarted API gets a new bank. Its ids must not repeat the earlier ones, since the application keeps
    // a provider payment id unique per merchant account.
    [Fact]
    public async Task AfterARestart_ThePaymentAndTransactionIds_DoNotRepeat()
    {
        MerchantCredentials merchant = Merchant();
        (_, string token) = await StartAsync(merchant);
        IReadOnlyDictionary<string, string> before = (await _control.SubmitCardAsync(token, Card("0000"), CancellationToken.None)).ReturnFields;

        var services = new ServiceCollection();
        services.AddSingleton<IPaymentLogWriter>(new RecordingWriter());
        services.AddPaymentSimulator(new Environment(Environments.Development), o => o.PageBaseUrl = PageBase);
        services.AddSingleton<ISimulatorNotificationSender>(new RecordingSender());
        using ServiceProvider restarted = services.BuildServiceProvider();
        IPaymentProvider provider = restarted.GetRequiredService<IEnumerable<IPaymentProvider>>().Single(p => p.Name == PaymentProviderName.Simulator);
        PaymentStartResult started = await provider.StartAsync(new PaymentStartRequest($"ORD-{Guid.NewGuid():N}"[..16], Hundred,
            Buyer,
            "https://app.test/api/payments/callback/simulator", "https://app.test/api/payments/return/simulator", "Pro, aylık"), merchant, CancellationToken.None);
        IReadOnlyDictionary<string, string> after = (await restarted.GetRequiredService<ISimulatorControl>()
            .SubmitCardAsync(started.ProviderToken!, Card("0000"), CancellationToken.None)).ReturnFields;

        Assert.StartsWith("SIM-P-", before["payment_id"]);
        Assert.StartsWith("SIM-T-", before["transaction_id"]);
        Assert.NotEqual(before["payment_id"], after["payment_id"]);
        Assert.NotEqual(before["transaction_id"], after["transaction_id"]);
    }

    [Fact]
    public void InProduction_TheSimulatorCannotBeRegistered()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddPaymentSimulator(new Environment(Environments.Production), _ => { }));
    }
}
