namespace Moe.Infrastructure.Shared.Api;

public static class ApiErrorMessages
{
    public const string ValidationFailed = "Some information needs to be corrected before you can continue.";
    public const string BadRequest = "We could not process this request. Please check the information and try again.";
    public const string Unauthorized = "Your session has expired. Please sign in again.";
    public const string Forbidden = "You do not have permission to perform this action.";
    public const string NotFound = "The requested record could not be found.";
    public const string Conflict = "This record conflicts with another action. Please refresh and try again.";
    public const string TooManyRequests = "Too many requests. Please wait a moment and try again.";
    public const string Unexpected = "Something went wrong. Please try again later.";

    public static string ForStatusCode(int statusCode, string? fallback = null)
        => statusCode switch
        {
            ApiResponseCodes.BadRequest => string.IsNullOrWhiteSpace(fallback) ? BadRequest : fallback,
            ApiResponseCodes.Unauthorized => Unauthorized,
            ApiResponseCodes.Forbidden => Forbidden,
            ApiResponseCodes.NotFound => NotFound,
            ApiResponseCodes.Conflict => Conflict,
            ApiResponseCodes.UnprocessableEntity => ValidationFailed,
            ApiResponseCodes.TooManyRequests => TooManyRequests,
            ApiResponseCodes.InternalServerError => Unexpected,
            ApiResponseCodes.BadGateway => Unexpected,
            _ => string.IsNullOrWhiteSpace(fallback) ? Unexpected : fallback
        };
}
