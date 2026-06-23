using Microsoft.AspNetCore.Http;

namespace Moe.Infrastructure.Shared.Exceptions;

public sealed class ForbiddenApiException(
    string message,
    string code = "FORBIDDEN") : ApiException(code, message, StatusCodes.Status403Forbidden);
