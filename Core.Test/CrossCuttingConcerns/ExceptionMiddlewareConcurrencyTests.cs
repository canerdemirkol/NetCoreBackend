using System.Text;
using Microsoft.AspNetCore.Http;
using Moq;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi.Middleware;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Logging.Abstraction;

namespace Core.Test.CrossCuttingConcerns;

/// <summary>
/// The middleware is a single instance shared by every request; failing requests running at the same
/// time must each get their own status and body (no cross-request writes, no crashed connections).
/// </summary>
public class ExceptionMiddlewareConcurrencyTests
{
    private const int RequestCount = 2000;

    [Fact]
    public async Task ConcurrentFailures_EachWriteTheirOwnResponse()
    {
        var middleware = new ExceptionMiddleware(
            context => throw new BusinessException(context.TraceIdentifier),
            new HttpContextAccessor(),
            new Mock<ILogger>().Object);
        DefaultHttpContext[] contexts = Enumerable.Range(0, RequestCount).Select(i =>
        {
            var context = new DefaultHttpContext { TraceIdentifier = $"request-{i:D4}" };
            context.Response.Body = new MemoryStream();
            return context;
        }).ToArray();

        await Parallel.ForEachAsync(contexts, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
            async (context, _) => await middleware.Invoke(context));

        Assert.All(contexts, context =>
        {
            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            string body = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
            Assert.Contains(context.TraceIdentifier, body);
            Assert.Equal(1, CountOf(body, "\"status\""));
        });
    }

    private static int CountOf(string text, string value)
    {
        int count = 0;
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
