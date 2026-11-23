namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

/// <summary>
/// The simulator's handle for the payment page, the card form and tests: submit a card (its last four digits pick the outcome), send a
/// held notification (once, twice or tampered), close the day (cancel before, refund after).
/// </summary>
public interface ISimulatorControl
{
    string RenderPaymentPage(string token);
    Task<SimulatorSubmitResult> SubmitCardAsync(string token, string cardNumber, CancellationToken cancellationToken);
    Task SendNotificationAsync(string orderNo, int times, bool tamper, CancellationToken cancellationToken);
    void CloseDay();
}
