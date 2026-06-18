using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed class RequestManualRunRequestValidator : AbstractValidator<RequestManualRunRequest>
{
    public RequestManualRunRequestValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null);
    }
}
