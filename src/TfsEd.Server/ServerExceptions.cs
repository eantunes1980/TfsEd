using System.Net;
using TfsEd.Core;
using TfsEd.Core.Workspaces;

namespace TfsEd.Server;

/// <summary>The server returned an error response.</summary>
public class TfsEdServerException(string message, HttpStatusCode? statusCode = null) : TfsEdException(message), IHasStatusCode
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

/// <summary>The personal access token was rejected.</summary>
public sealed class TfsEdAuthenticationException(string message, HttpStatusCode? statusCode = null)
    : TfsEdServerException(message, statusCode);
