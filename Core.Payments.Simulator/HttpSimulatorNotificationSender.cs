namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

/// <summary>Posts the notification as a form, like the real provider, and returns the application's answer.</summary>
public sealed class HttpSimulatorNotificationSender : ISimulatorNotificationSender
{
    public const string ClientName = "PaymentSimulatorNotifier";
    private readonly IHttpClientFactory _clients;

    public HttpSimulatorNotificationSender(IHttpClientFactory clients)
    {
        _clients = clients;
    }

    public async Task<string> SendAsync(string url, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _clients.CreateClient(ClientName).PostAsync(url, new FormUrlEncodedContent(fields), cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
