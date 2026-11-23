namespace NetCoreBackend.NArchitecture.Core.Payments.Masking;

/// <summary>A body fit for the log, and the personal data taken out of it (by path: "buyer.email").</summary>
public sealed record MaskedPayload(string? Body, IReadOnlyDictionary<string, string> PersonalData);
