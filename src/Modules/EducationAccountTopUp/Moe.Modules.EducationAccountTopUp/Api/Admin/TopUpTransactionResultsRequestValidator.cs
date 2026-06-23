using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed class TopUpTransactionResultsRequestValidator
    : AbstractValidator<TopUpTransactionResultsRequest>
{
    public TopUpTransactionResultsRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(TopUpReadValidationRules.BeValidEnumValue<TopUpTransactionStatusCode>)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Status must be a valid top-up transaction status.");

        RuleFor(x => x.StudentOrAccountSearch).MaximumLength(100);
        RuleFor(x => x.Reason).MaximumLength(200);
        RuleFor(x => x.Page)
            .InclusiveBetween(1, TopUpReadValidationRules.MaxPageNumber);
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, TopUpReadValidationRules.MaxPageSize);
        RuleFor(x => x)
            .Must(x => TopUpReadValidationRules.HasValidDateRange(
                x.DateFromUtc,
                x.DateToUtc))
            .WithMessage("DateFromUtc must be earlier than DateToUtc.");
    }
}
