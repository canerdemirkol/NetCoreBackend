namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>A notification or a browser return as it arrived: its fields and the allow-listed headers.</summary>
public sealed record IncomingPaymentMessage(IReadOnlyDictionary<string, string> Fields, IReadOnlyDictionary<string, string> Headers);
