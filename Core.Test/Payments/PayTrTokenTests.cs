using NetCoreBackend.NArchitecture.Core.Payments;
using NetCoreBackend.NArchitecture.Core.Payments.PayTr;

namespace Core.Test.Payments;

/// <summary>
/// PayTR's four HMAC formulas (dev.paytr.com: iFrame API steps 1 and 2, status inquiry, refund) and its basket. The expected values were
/// computed independently (Python hmac, base64) from the documented field orders.
/// </summary>
public class PayTrTokenTests
{
    private const string MerchantId = "123456";
    private const string MerchantKey = "key-1";
    private const string MerchantSalt = "salt-1";
    private const string OrderNo = "PO20261123A7K3Q9";
    private const string Basket = "W1siUHJvLCBheWxcdTAxMzFrIiwiMTAwLjUwIiwxXV0=";

    [Fact]
    public void TheBasket_IsBase64OfTheJsonList_OfNamePriceAndQuantity()
    {
        Assert.Equal(Basket, PayTrBasket.Encode("Pro, aylık", Money.Of(100.5m)));
    }

    [Fact]
    public void TheStartToken_SignsTheTenFieldsThenTheSalt()
    {
        Assert.Equal("JyVP3n4Nm35wM1SEx5bMSjwkSG7CjoWURNssbaUsts0=", PayTrToken.ForStart(MerchantId, MerchantKey, MerchantSalt, "88.230.1.1", OrderNo,
            "ayse@test.local", "10050", Basket, "1", "0", "TL", "1"));
    }

    [Fact]
    public void TheNotificationHash_IsOrderSaltStatusAndTotal()
    {
        Assert.Equal("66yOg32c6ZRrfnpstNxMLrvZktwwklkrtkYG4Tr8VMI=", PayTrToken.ForNotification(MerchantKey, MerchantSalt, OrderNo, "success", "10050"));
    }

    [Fact]
    public void TheStatusQueryToken_IsMerchantOrderAndSalt()
    {
        Assert.Equal("K6+JrC6d9q/TNW5+sVSKLsxIpWtM6inxGv1Tpz5NG8I=", PayTrToken.ForStatusQuery(MerchantId, MerchantKey, MerchantSalt, OrderNo));
    }

    [Fact]
    public void TheRefundToken_IsMerchantOrderAmountAndSalt()
    {
        Assert.Equal("mbjiJwJzo0o0w/ifW9xvxUJ0fHNtnhgOvMgOlK4YzCw=", PayTrToken.ForRefund(MerchantId, MerchantKey, MerchantSalt, OrderNo, "50.25"));
    }
}
