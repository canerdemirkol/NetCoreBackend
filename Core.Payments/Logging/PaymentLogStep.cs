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
    StoredCardCharge = 21,
    StoredCardDeleted = 22,
}
