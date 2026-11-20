using System.Net;
using NetCoreBackend.NArchitecture.Core.Payments;
using NetCoreBackend.NArchitecture.Core.Payments.Logging;
using NetCoreBackend.NArchitecture.Core.Payments.Masking;

namespace Core.Test.Payments;

/// <summary>Card numbers never reach the log, secrets are starred, personal data is kept apart.</summary>
public class PaymentDataMaskerTests
{
    [Theory]
    [InlineData("4111111111111111", true)]       // Luhn-valid
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("4111111111111112", false)]      // fails Luhn: an order number, not a card
    [InlineData("123456", false)]
    public void ACardNumber_IsRecognised(string value, bool isCard)
    {
        Assert.Equal(isCard, PaymentDataMasker.IsCardNumber(value));
    }

    [Fact]
    public void AJsonBody_LosesCards_StarsSecrets_AndSeparatesPersonalData()
    {
        const string body = """{"apiKey":"k-1","order":{"cardNumber":"4111111111111111","hash":"abc"},"buyer":{"email":"a@b.c","name":"Ayşe"},"price":"100.00"}""";

        MaskedPayload masked = PaymentDataMasker.Mask(body, "application/json");

        Assert.DoesNotContain("4111111111111111", masked.Body);
        Assert.DoesNotContain("k-1", masked.Body);
        Assert.DoesNotContain("abc", masked.Body);
        Assert.DoesNotContain("a@b.c", masked.Body);
        Assert.Contains("100.00", masked.Body);
        Assert.Equal("a@b.c", masked.PersonalData["buyer.email"]);
        Assert.Equal("Ayşe", masked.PersonalData["buyer.name"]);
    }

    [Fact]
    public void AFormBody_IsMaskedTheSameWay()
    {
        MaskedPayload masked = PaymentDataMasker.Mask("merchant_oid=ORD-1&hash=xyz&email=a%40b.c&note=4111111111111111", "application/x-www-form-urlencoded");

        Assert.Contains("merchant_oid=ORD-1", masked.Body);
        Assert.DoesNotContain("xyz", masked.Body);
        Assert.DoesNotContain("4111111111111111", masked.Body);
        Assert.Equal("a@b.c", masked.PersonalData["email"]);
    }

    [Fact]
    public void ACardNumberInFreeText_IsRemovedToo()
    {
        MaskedPayload masked = PaymentDataMasker.Mask("error: card 4111-1111-1111-1111 declined", "text/plain");

        Assert.DoesNotContain("1111-1111-1111", masked.Body);
        Assert.Contains("declined", masked.Body);
    }
}

public class PaymentLoggingHandlerTests
{
    private sealed class RecordingWriter : IPaymentLogWriter
    {
        public List<PaymentLogEntry> Entries { get; } = [];
        public bool Fail { get; set; }

        public Task WriteAsync(PaymentLogEntry entry, CancellationToken cancellationToken)
        {
            if (Fail)
                throw new InvalidOperationException("log store down");
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class StubProvider(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(answer(request));
        }
    }

    private static readonly PaymentLogContext Context = new()
    {
        OrderId = Guid.NewGuid(), Step = PaymentLogStep.StartRequestSent, ResponseStep = PaymentLogStep.StartResponseReceived,
        Provider = PaymentProviderName.Simulator, Environment = PaymentEnvironment.Test, CorrelationId = "corr-1",
        SignatureHeaderNames = ["X-Signature"],
    };

    private static (HttpClient Client, RecordingWriter Writer, StubProvider Provider) Build(Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var writer = new RecordingWriter();
        var provider = new StubProvider(answer);
        var client = new HttpClient(new PaymentLoggingHandler(writer) { InnerHandler = provider }) { BaseAddress = new Uri("https://sandbox.provider.test/") };
        return (client, writer, provider);
    }

    private static HttpRequestMessage Start()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/start") { Content = new StringContent("""{"apiKey":"secret-key","price":"100.00"}""", System.Text.Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Signature", "sig-value");
        request.Headers.Add("Authorization", "Bearer token-value");
        request.Headers.Add("Cookie", "session=1");
        return request;
    }

    [Fact]
    public async Task ARequest_AndItsResponse_AreTwoRows_BeforeAndAfter()
    {
        (HttpClient client, RecordingWriter writer, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"status":"success","token":"t-1"}""") });

        using (PaymentLogScope.Begin(Context))
            await client.SendAsync(Start());

        Assert.Equal(2, writer.Entries.Count);
        PaymentLogEntry sent = writer.Entries[0], received = writer.Entries[1];
        Assert.Equal((PaymentLogStep.StartRequestSent, PaymentLogDirection.Outgoing), (sent.Step, sent.Direction));
        Assert.Equal("https://sandbox.provider.test", sent.BaseAddress);
        Assert.True(sent.TestFlag);
        Assert.Contains("100.00", sent.Body);
        Assert.DoesNotContain("secret-key", sent.Body);
        Assert.Equal("present", sent.Headers["X-Signature"]);
        Assert.False(sent.Headers.ContainsKey("Authorization"));
        Assert.False(sent.Headers.ContainsKey("Cookie"));
        Assert.Equal((PaymentLogStep.StartResponseReceived, 200), (received.Step, received.HttpStatus));
        Assert.DoesNotContain("t-1", received.Body);
        Assert.NotNull(received.DurationMs);
        Assert.Equal("corr-1", received.CorrelationId);
    }

    [Fact]
    public async Task ANetworkFailure_IsARowToo_AndStillFails()
    {
        (HttpClient client, RecordingWriter writer, _) = Build(_ => throw new HttpRequestException("DNS failure"));

        using (PaymentLogScope.Begin(Context))
            await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(Start()));

        Assert.Equal(2, writer.Entries.Count);
        Assert.Equal(PaymentLogOutcome.Failed, writer.Entries[1].Outcome);
        Assert.Equal(nameof(HttpRequestException), writer.Entries[1].ExceptionType);
    }

    [Fact]
    public async Task ACallOutsideAScope_IsRefused_AndNothingIsSent()
    {
        (HttpClient client, _, StubProvider provider) = Build(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(Start()));

        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task WhenTheLogCannotBeWritten_TheRequestIsNotSent()
    {
        (HttpClient client, RecordingWriter writer, StubProvider provider) = Build(_ => new HttpResponseMessage(HttpStatusCode.OK));
        writer.Fail = true;

        using (PaymentLogScope.Begin(Context))
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(Start()));

        Assert.Equal(0, provider.Calls);
    }
}
