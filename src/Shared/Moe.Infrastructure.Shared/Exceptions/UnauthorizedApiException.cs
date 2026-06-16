using Microsoft.AspNetCore.Http;

namespace Moe.Infrastructure.Shared.Exceptions;

public sealed class UnauthorizedApiException(
    string message,
    string code = "UNAUTHORIZED") : ApiException(code, message, StatusCodes.Status401Unauthorized);
