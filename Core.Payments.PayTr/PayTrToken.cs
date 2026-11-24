using System.Security.Cryptography;
using System.Text;

namespace NetCoreBackend.NArchitecture.Core.Payments.PayTr;

/// <summary>
/// PayTR's request tokens and notification hash (dev.paytr.com): base64 HMAC-SHA256 with the merchant key over the documented fields in
/// order, the merchant salt last (first after the order number in the notification).
/// </summary>
public static class PayTrToken
{
    public static string ForStart(string merchantId, string merchantKey, string merchantSalt, string userIp, string merchantOid, string email,
        string paymentAmount, string userBasket, string noInstallment, string maxInstallment, string currency, string testMode) =>
        Hmac(merchantKey, merchantId + userIp + merchantOid + email + paymentAmount + userBasket + noInstallment + maxInstallment + currency + testMode
            + merchantSalt);

    public static string ForNotification(string merchantKey, string merchantSalt, string merchantOid, string status, string totalAmount) =>
        Hmac(merchantKey, merchantOid + merchantSalt + status + totalAmount);

    public static string ForStatusQuery(string merchantId, string merchantKey, string merchantSalt, string merchantOid) =>
        Hmac(merchantKey, merchantId + merchantOid + merchantSalt);

    public static string ForRefund(string merchantId, string merchantKey, string merchantSalt, string merchantOid, string returnAmount) =>
        Hmac(merchantKey, merchantId + merchantOid + returnAmount + merchantSalt);

    // E13-14b: "İşlem Dökümü" (dev.paytr.com/islem-dokumu).
    public static string ForTransactionReport(string merchantId, string merchantKey, string merchantSalt, string startDate, string endDate) =>
        Hmac(merchantKey, merchantId + startDate + endDate + merchantSalt);

    /// <summary>Compares in constant time; a missing hash never matches.</summary>
    public static bool Matches(string expected, string? given) =>
        given is not null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(given));

    private static string Hmac(string key, string message) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(message)));
}
