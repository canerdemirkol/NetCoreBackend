namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

public sealed class SimulatorOptions
{
    /// <summary>The credentials' extra that picks the flow (<see cref="SimulatorFlow"/> name; HostedForm when absent).</summary>
    public const string FlowKey = "flow";

    /// <summary>Where the application serves the simulator's payment page; the page of a payment is <c>{PageBaseUrl}/{token}</c>.</summary>
    public string PageBaseUrl { get; set; } = "";
}
