using FluentValidation;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;

public sealed class RequestManualRunCommandValidator : AbstractValidator<RequestManualRunCommand>
{
    public RequestManualRunCommandValidator()
    {
        RuleFor(x => x.CampaignId).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Note).MaximumLength(500).When(x => x.Note is not null);
    }
}
