using Microsoft.AspNetCore.Http;

namespace Moe.Infrastructure.Shared.Exceptions;

public sealed class BadRequestApiException(
    string message,
    string code = "BAD_REQUEST") : ApiException(code, message, StatusCodes.Status400BadRequest);
