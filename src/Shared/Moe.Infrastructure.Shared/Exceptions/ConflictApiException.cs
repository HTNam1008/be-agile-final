using Microsoft.AspNetCore.Http;

namespace Moe.Infrastructure.Shared.Exceptions;

public sealed class ConflictApiException(
    string message,
    string code = "CONFLICT") : ApiException(code, message, StatusCodes.Status409Conflict);
