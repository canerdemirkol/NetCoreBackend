namespace NetCoreBackend.NArchitecture.Core.Payments;

/// <summary>E13-14b: a day's transactions as the provider reports them, or why they could not be read.</summary>
public sealed record TransactionReportResult(bool Succeeded, IReadOnlyList<ProviderTransaction> Transactions, string? ErrorCode, string? ErrorMessage)
{
    public static TransactionReportResult Of(IReadOnlyList<ProviderTransaction> transactions) => new(true, transactions, null, null);

    public static TransactionReportResult Failure(string code, string? message) => new(false, [], code, message);
}
