namespace Versioner.Services.Parse;

public sealed class AppReleaseException : Exception
{
    public AppReleaseException(string message) : base(message)
    {
    }

    public AppReleaseException(string message, Exception inner) : base(message, inner)
    {
    }
}
