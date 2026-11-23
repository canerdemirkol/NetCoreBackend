namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

internal sealed record GatewayAnswer(bool Succeeded, string? ErrorCode = null, string? ErrorMessage = null, string? Token = null,
    string? State = null, long? AmountMinor = null, string? Currency = null, string? PaymentId = null, string? TransactionId = null,
    string? Reference = null);
