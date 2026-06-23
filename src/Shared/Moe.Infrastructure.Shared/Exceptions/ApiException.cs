namespace Moe.Infrastructure.Shared.Exceptions;

public abstract class ApiException : Exception
{
    protected ApiException(
        string code,
        string message,
        int statusCode) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
