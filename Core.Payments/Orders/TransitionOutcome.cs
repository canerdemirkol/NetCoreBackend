namespace NetCoreBackend.NArchitecture.Core.Payments.Orders;

public enum TransitionOutcome
{
    Applied,
    // Already handled (a repeated notification) or nothing to change.
    NoEffect,
    // The message cannot be trusted (bad signature).
    Rejected,
}
