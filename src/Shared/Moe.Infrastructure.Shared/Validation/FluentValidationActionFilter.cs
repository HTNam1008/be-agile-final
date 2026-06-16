using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Moe.Infrastructure.Shared.Api;

namespace Moe.Infrastructure.Shared.Validation;

public sealed class FluentValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (object? argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            Type validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            object? validator = context.HttpContext.RequestServices.GetService(validatorType);

            if (validator is null)
            {
                continue;
            }

            IValidationContext validationContext = new ValidationContext<object>(argument);
            var validationResult = await ((IValidator)validator).ValidateAsync(
                validationContext,
                context.HttpContext.RequestAborted);

            if (validationResult.IsValid)
            {
                continue;
            }

            string[] errors = validationResult.Errors
                .Select(error => error.ErrorMessage)
                .Distinct()
                .ToArray();

            context.Result = new BadRequestObjectResult(ApiResponse<object>.Fail(
                "Validation failed.",
                errors,
                ApiResponseCodes.BadRequest,
                context.HttpContext.TraceIdentifier));
            return;
        }

        await next();
    }
}
