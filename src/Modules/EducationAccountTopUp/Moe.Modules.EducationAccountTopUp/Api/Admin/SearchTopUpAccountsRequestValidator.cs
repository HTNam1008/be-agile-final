using FluentValidation;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.SearchAccounts;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

public sealed class SearchTopUpAccountsRequestValidator : AbstractValidator<SearchTopUpAccountsRequest>
{
    public SearchTopUpAccountsRequestValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.OrganizationId).GreaterThan(0).When(x => x.OrganizationId.HasValue);
        RuleFor(x => x.SchoolingStatusCode).MaximumLength(30);
        RuleFor(x => x.LevelCode).MaximumLength(30);
        RuleFor(x => x.ClassCode).MaximumLength(30);
        RuleFor(x => x.AccountStatusCode).MaximumLength(30);
        RuleFor(x => x.AgeFrom).InclusiveBetween(0, 120).When(x => x.AgeFrom.HasValue);
        RuleFor(x => x.AgeTo).InclusiveBetween(0, 120).When(x => x.AgeTo.HasValue);
        RuleFor(x => x.BalanceFrom).GreaterThanOrEqualTo(0).When(x => x.BalanceFrom.HasValue);
        RuleFor(x => x.BalanceTo).GreaterThanOrEqualTo(0).When(x => x.BalanceTo.HasValue);
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, SearchTopUpAccountsValidator.MaxPageSize);
        RuleFor(x => x).Must(x => !x.AgeFrom.HasValue || !x.AgeTo.HasValue || x.AgeFrom <= x.AgeTo)
            .WithMessage("AgeFrom must be less than or equal to AgeTo.");
        RuleFor(x => x).Must(x => !x.BalanceFrom.HasValue || !x.BalanceTo.HasValue || x.BalanceFrom <= x.BalanceTo)
            .WithMessage("BalanceFrom must be less than or equal to BalanceTo.");
    }
}
