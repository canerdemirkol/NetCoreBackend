using Microsoft.AspNetCore.Http;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.Abstractions;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.CorrelationId.WebApi.Accessors;

namespace Core.Test.CrossCuttingConcerns;

/// <summary>
/// Background work (a scheduled job's run) has no HTTP request to carry a correlation id: a run opens one, everything written in it carries
/// it, and the accessor falls back to it when there is no request.
/// </summary>
public class BackgroundCorrelationTests
{
    // Where CorrelationIdMiddleware keeps the request's id (its constant is internal).
    private const string RequestItemsKey = "CorrelationId";

    [Fact]
    public async Task ARun_HoldsOneId_ANestedRunRestoresTheOuterOne_AndParallelRunsDoNotShare()
    {
        Assert.Null(BackgroundCorrelation.Current);
        string? outer, inner, afterInner;
        using (BackgroundCorrelation.Begin("payments"))
        {
            outer = BackgroundCorrelation.Current;
            using (BackgroundCorrelation.Begin("subscriptions"))
                inner = BackgroundCorrelation.Current;
            afterInner = BackgroundCorrelation.Current;
        }

        string?[] parallel = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            using (BackgroundCorrelation.Begin("payments"))
            {
                await Task.Yield();
                return BackgroundCorrelation.Current;
            }
        })));

        Assert.StartsWith("job-payments-", outer);
        Assert.StartsWith("job-subscriptions-", inner);
        Assert.Equal(outer, afterInner);
        Assert.Null(BackgroundCorrelation.Current);
        Assert.NotEqual(parallel[0], parallel[1]);
    }

    private static ICorrelationIdAccessor AccessorWith(string? requestId)
    {
        HttpContext? context = null;
        if (requestId is not null)
        {
            context = new DefaultHttpContext();
            context.Items[RequestItemsKey] = requestId;
        }
        return new CorrelationIdAccessor(new HttpContextAccessor { HttpContext = context });
    }

    [Fact]
    public void TheAccessor_PrefersTheRequestsId_ThenTheRunsId_ThenNone()
    {
        Assert.Null(AccessorWith(null).CorrelationId);
        using (BackgroundCorrelation.Begin("payments"))
        {
            Assert.Equal("req-1", AccessorWith("req-1").CorrelationId);
            Assert.Equal(BackgroundCorrelation.Current, AccessorWith(null).CorrelationId);
        }
    }
}
