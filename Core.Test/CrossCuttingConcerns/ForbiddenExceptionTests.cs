using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using NetCoreBackend.NArchitecture.Core.Application.Pipelines.Authorization;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Handlers;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.WebApi.Handlers;
using NetCoreBackend.NArchitecture.Core.Mediation.Abstractions;

namespace Core.Test.CrossCuttingConcerns;

/// <summary>
/// 401 means "who are you?" (a client refreshes its token and retries); 403 means "known, but not allowed" (retrying
/// cannot help). A forbidden request must reach the client as 403 without breaking handlers written before the split.
/// </summary>
public class ForbiddenExceptionTests
{
    private const string RequiredRole = "Users.Create";

    [Fact]
    public async Task AForbiddenRequest_Answers403_WithItsMessage()
    {
        (int status, string body) = await HandleAsync(new ForbiddenException("Not allowed here."));

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Contains("\"title\":\"Forbidden\"", body);
        Assert.Contains("Not allowed here.", body);
        Assert.Contains("\"status\":403", body);
    }

    [Fact]
    public async Task AForbiddenRequestWithACode_CarriesTheCode_SoAClientCanTellTheReason()
    {
        (int status, string body) = await HandleAsync(new ForbiddenException("Subscription expired.") { Code = "SubscriptionExpired" });

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Contains("\"code\":\"SubscriptionExpired\"", body);
        Assert.Contains("Subscription expired.", body);
    }

    [Fact]
    public async Task AForbiddenRequestWithoutACode_HasNoCodeField()
    {
        (_, string body) = await HandleAsync(new ForbiddenException("Not allowed here."));

        Assert.DoesNotContain("\"code\"", body);
    }

    [Fact]
    public async Task AnUnauthenticatedRequest_StillAnswers401()
    {
        (int status, string body) = await HandleAsync(new AuthorizationException("You are not authenticated."));

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.Contains("\"status\":401", body);
    }

    [Fact]
    public void AForbiddenException_IsAnAuthorizationException_SoExistingCatchBlocksStillSeeIt()
    {
        Assert.IsAssignableFrom<AuthorizationException>(new ForbiddenException("x"));
    }

    [Fact]
    public async Task AHandlerWrittenBeforeTheSplit_HandlesAForbiddenRequestAsBefore()
    {
        var handler = new HandlerWithoutForbidden();

        await handler.HandleException(new ForbiddenException("x"));

        Assert.Equal(nameof(AuthorizationException), handler.Handled);
    }

    [Fact]
    public async Task AMissingRole_IsForbidden()
    {
        var behavior = BehaviorFor(new Claim(ClaimTypes.Role, "Users.Read"));

        ForbiddenException thrown = await Assert.ThrowsAsync<ForbiddenException>(() =>
            behavior.Handle(new SecuredProbe(), _ => Task.FromResult("ran"), CancellationToken.None));

        Assert.Equal("You are not authorized.", thrown.Message);
    }

    [Fact]
    public async Task NoIdentity_IsUnauthenticated_Not_Forbidden()
    {
        var behavior = BehaviorFor();

        await Assert.ThrowsAsync<AuthorizationException>(() =>
            behavior.Handle(new SecuredProbe(), _ => Task.FromResult("ran"), CancellationToken.None));
    }

    [Fact]
    public async Task TheRequiredRole_Passes()
    {
        var behavior = BehaviorFor(new Claim(ClaimTypes.Role, RequiredRole));

        Assert.Equal("ran", await behavior.Handle(new SecuredProbe(), _ => Task.FromResult("ran"), CancellationToken.None));
    }

    private static async Task<(int Status, string Body)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await new HttpExceptionHandler { Response = context.Response }.HandleException(exception);
        return (context.Response.StatusCode, Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
    }

    private static AuthorizationBehavior<SecuredProbe, string> BehaviorFor(params Claim[] claims)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, claims.Length > 0 ? "test" : null)) };
        return new AuthorizationBehavior<SecuredProbe, string>(new HttpContextAccessor { HttpContext = context });
    }

    private sealed record SecuredProbe : IRequest<string>, ISecuredRequest
    {
        public string[] Roles => [RequiredRole];
    }

    private sealed class HandlerWithoutForbidden : ExceptionHandler
    {
        public string? Handled { get; private set; }

        public override Task HandleException(BusinessException businessException) => Record(nameof(BusinessException));
        public override Task HandleException(ValidationException validationException) => Record(nameof(ValidationException));
        public override Task HandleException(AuthorizationException authorizationException) => Record(nameof(AuthorizationException));
        public override Task HandleException(NotFoundException notFoundException) => Record(nameof(NotFoundException));
        protected override Task HandleUnknownException(Exception exception) => Record("unknown");

        private Task Record(string name)
        {
            Handled = name;
            return Task.CompletedTask;
        }
    }
}
