namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

public enum SimulatorFlow
{
    // Like iyzico: a hosted payment page; the browser posts a signed result back.
    HostedForm,
    // Like PayTR: an embedded page; the provider posts a signed notification to the server and expects "OK".
    ServerNotification,
}
