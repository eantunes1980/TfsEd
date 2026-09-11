namespace TfsEd.Core;

/// <summary>
/// Base class for all expected, user-facing errors. The CLI prints the message without a stack trace.
/// </summary>
public class TfsEdException : Exception
{
    public TfsEdException(string message) : base(message) { }

    public TfsEdException(string message, Exception innerException) : base(message, innerException) { }
}
