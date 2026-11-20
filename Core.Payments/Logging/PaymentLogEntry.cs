namespace NetCoreBackend.NArchitecture.Core.Payments.Logging;

/// <summary>Every step of a payment, in order (the analysis' 20 steps); each is at least one log row.</summary>
public enum PaymentLogStep
{
    OrderCreated = 1,
    ConsentRecorded = 2,
    StartRequestSent = 3,
    StartResponseReceived = 4,
    PaymentPageIssued = 5,
    PaymentPageEvent = 6,
    ReturnReceived = 7,
    NotificationReceived = 8,
    SignatureChecked = 9,
    StatusQuery = 10,
    Compared = 11,
    StatusChanged = 12,
    ResponseToProvider = 13,
    SubscriptionApplied = 14,
    EmailSent = 15,
    Reconciliation = 16,
    CancelOrRefund = 17,
    MarkedPaidManually = 18,
    UnmatchedMessage = 19,
    Error = 20,
}

public enum PaymentLogDirection
{
    Outgoing,
    Incoming,
    Internal,
    Ui,
}

public enum PaymentLogOutcome
{
    Succeeded,
    Failed,
    Rejected,
    NoEffect,
}

/// <summary>
/// One row of the payment transaction log. Append-only: a response is a new row, never an update of the request's. Bodies are masked
/// (<see cref="Masking.PaymentDataMasker"/>); personal data is in <see cref="PersonalData"/>, apart from the body; a stack trace goes to the
/// system log under <see cref="CorrelationId"/>, only the exception's type and message come here.
/// </summary>
public sealed record PaymentLogEntry
{
    // Identity and trace.
    public Guid? OrderId { get; init; }
    public int AttemptNo { get; init; } = 1;
    public string? CorrelationId { get; init; }
    public string? IdempotencyKey { get; init; }

    // Stage.
    public PaymentLogStep Step { get; init; }
    public PaymentLogDirection Direction { get; init; }
    public PaymentLogOutcome Outcome { get; init; }

    // Who.
    public Guid? PayerTenantId { get; init; }
    public Guid? OwnerTenantId { get; init; }
    public Guid? UserId { get; init; }
    public Guid? ImpersonatorUserId { get; init; }
    public string? ClientIp { get; init; }

    // Which POS.
    public PaymentProviderName? Provider { get; init; }
    public PaymentEnvironment? Environment { get; init; }
    public string? BaseAddress { get; init; }
    public bool? TestFlag { get; init; }
    public Guid? MerchantAccountId { get; init; }
    public int? CredentialVersion { get; init; }
    public string? CredentialFingerprint { get; init; }

    // Request or response.
    public string? Endpoint { get; init; }
    public string? Method { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public string? Body { get; init; }
    public IReadOnlyDictionary<string, string> PersonalData { get; init; } = new Dictionary<string, string>();
    public int? HttpStatus { get; init; }
    public long? DurationMs { get; init; }

    // Our answer and the order.
    public string? ResponseToProvider { get; init; }
    public string? StatusBefore { get; init; }
    public string? StatusAfter { get; init; }

    // Result.
    public string? ProviderPaymentId { get; init; }
    public string? ProviderTransactionId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool? SignatureValid { get; init; }
    public long? AmountMinor { get; init; }
    public string? Currency { get; init; }
    public bool MismatchFlag { get; init; }
    public int? Installment { get; init; }
    public string? CardLast4 { get; init; }
    public string? CardFamily { get; init; }

    // Error.
    public string? ExceptionType { get; init; }
    public string? ExceptionMessage { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

/// <summary>
/// Stores log rows, append-only (the application: its own table, insert and select only). A failure throws: a payment step whose log cannot
/// be written does not go ahead.
/// </summary>
public interface IPaymentLogWriter
{
    Task WriteAsync(PaymentLogEntry entry, CancellationToken cancellationToken);
}
