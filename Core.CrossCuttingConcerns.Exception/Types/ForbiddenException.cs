namespace NetCoreBackend.NArchitecture.Core.CrossCuttingConcerns.Exception.Types;

/// <summary>
/// The caller is known but may not do this (HTTP 403); a new token cannot help. <see cref="AuthorizationException"/> alone
/// means the caller is not known (HTTP 401). Derives from it so existing catch blocks and handlers keep seeing it.
/// </summary>
public class ForbiddenException : AuthorizationException
{
    public ForbiddenException() { }

    public ForbiddenException(string? message)
        : base(message) { }

    public ForbiddenException(string? message, System.Exception? innerException)
        : base(message, innerException) { }
}
