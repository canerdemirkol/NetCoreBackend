namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

/// <summary>Sends a notification to the application's callback address and returns its answer (the real one posts a form over HTTP).</summary>
public interface ISimulatorNotificationSender
{
    Task<string> SendAsync(string url, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken);
}
