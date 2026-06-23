using Microsoft.AspNetCore.Http;

namespace Moe.Infrastructure.Shared.Exceptions;

public sealed class NotFoundApiException(
    string message,
    string code = "NOT_FOUND") : ApiException(code, message, StatusCodes.Status404NotFound);
