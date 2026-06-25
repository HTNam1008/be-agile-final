using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Moe.Modules.FasPayment.Domain.Fas;

namespace Moe.Modules.FasPayment.Api;

public sealed class FasApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (status, code) = context.Exception switch
        {
            KeyNotFoundException => (404, Message(context.Exception, "FAS.NOT_FOUND")),
            UnauthorizedAccessException => (401, Message(context.Exception, "FAS.AUTHENTICATION_REQUIRED")),
            ArgumentException => (422, Message(context.Exception, "FAS.VALIDATION_ERROR")),
            DomainException => (422, Message(context.Exception, "FAS.INVALID_STATE")),
            InvalidOperationException => (409, Message(context.Exception, "FAS.CONFLICT")),
            _ => (0, string.Empty)
        };
        if (status == 0) return;
        context.Result = new ObjectResult(new { success = false, code, message = Friendly(context.Exception.Message), traceId = context.HttpContext.TraceIdentifier }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
    private static string Message(Exception ex, string fallback) => ex.Message.StartsWith("FAS.", StringComparison.Ordinal) ? ex.Message : fallback;
    private static string Friendly(string message) => message.StartsWith("FAS.", StringComparison.Ordinal) ? message.Replace('.', ' ').ToLowerInvariant() : message;
}
