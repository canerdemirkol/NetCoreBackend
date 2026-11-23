namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

/// <summary>What the browser does after a card was submitted: post <see cref="ReturnFields"/> to <see cref="ReturnUrl"/> (the page posts itself).</summary>
public sealed record SimulatorSubmitResult(string ReturnUrl, IReadOnlyDictionary<string, string> ReturnFields, string AutoPostHtml);
