using FluentValidation;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Validation;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;

namespace Moe.Modules.FasPayment.Application.AdminFasSchemes;

internal sealed class CreateFasSchemeRequestValidator : AbstractValidator<CreateFasSchemeRequest>, IValidationFailureStatusCodeProvider
{
    public int ValidationFailureStatusCode => ApiResponseCodes.UnprocessableEntity;

    public CreateFasSchemeRequestValidator()
    {
        RuleFor(x => x.SchemeCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.GrantCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));
        RuleFor(x => x.EndDate).NotEqual(default(DateOnly));
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        RuleFor(x => x.CourseIds).NotNull().Must(x => x is not null && x.All(id => id > 0) && x.Distinct().Count() == x.Count).WithMessage("Course IDs must be positive and unique.");
        RuleFor(x => x.Tiers).NotNull().NotEmpty();
        RuleFor(x => x).Custom(ValidateStructure);
    }

    private static void ValidateStructure(CreateFasSchemeRequest request, ValidationContext<CreateFasSchemeRequest> context)
    {
        if (request.Tiers is null) return;
        ValidateOrders(request.Tiers.Where(x => x is not null).Select(x => x.DisplayOrder), "tiers", context);
        foreach (CreateFasTierRequest? tier in request.Tiers)
        {
            if (tier is null)
            {
                context.AddFailure("Tiers", "Tier entries cannot be null.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(tier.Label) || tier.Label.Length > 255) context.AddFailure("Tiers", "Each tier requires a label of at most 255 characters.");
            if (tier.SubsidyType is not ("FIXED" or "PERCENTAGE")) context.AddFailure("Tiers", "Tier subsidy type must be FIXED or PERCENTAGE.");
            if (tier.SubsidyValue < 0 || tier.SubsidyType == "PERCENTAGE" && tier.SubsidyValue > 100) context.AddFailure("Tiers", "Tier subsidy value is outside the allowed range.");
            
            if (tier.Criteria is null || tier.Criteria.Count == 0)
            {
                context.AddFailure("Tiers", "Each tier must provide at least one criteria rule.");
                continue;
            }
            if (tier.Criteria.Any(value => value is null))
            {
                context.AddFailure("Tiers", "Criteria cannot contain null entries.");
                continue;
            }
            
            ValidateOrders(tier.Criteria.Select(x => x.DisplayOrder), "criteria", context);
            var criteriaList = tier.Criteria.OrderBy(x => x.DisplayOrder).ToArray();
            for (int i = 0; i < criteriaList.Length; i++)
            {
                FasTierCriteriaRequest item = criteriaList[i];
                if (item.CriteriaType is not ("AGE" or "GDP" or "PCI" or "NATIONALITY")) context.AddFailure("Tiers", $"Unsupported criteria type '{item.CriteriaType}'.");
                bool last = i == criteriaList.Length - 1;
                if (last ? item.ConnectorToNext is not null : item.ConnectorToNext is not ("AND" or "OR")) context.AddFailure("Tiers", "Only the final connector may be null; earlier connectors must be AND or OR.");
                
                if (item.CriteriaType == "NATIONALITY")
                {
                    if (item.NumberFrom.HasValue || item.NumberTo.HasValue || item.Nationalities is null || item.Nationalities.Count == 0 || item.Nationalities.Any(x => !Moe.Modules.FasPayment.Domain.Fas.FasNationalities.All.Contains(x))) context.AddFailure("Tiers", "Nationality criteria require at least one supported nationality and no numeric bounds.");
                }
                else if (!item.NumberFrom.HasValue || !item.NumberTo.HasValue || item.NumberFrom > item.NumberTo || item.Nationalities?.Count > 0) context.AddFailure("Tiers", "Numeric criteria require a valid range and no nationalities.");
            }
        }
    }

    private static void ValidateOrders(IEnumerable<int> orders, string name, ValidationContext<CreateFasSchemeRequest> context)
    {
        int[] values = orders.Order().ToArray();
        if (!values.SequenceEqual(Enumerable.Range(1, values.Length))) context.AddFailure(name, "Display orders must be unique and contiguous from 1.");
    }
}

internal sealed class ListFasSchemesRequestValidator : AbstractValidator<ListFasSchemesRequest>, IValidationFailureStatusCodeProvider
{
    public int ValidationFailureStatusCode => ApiResponseCodes.UnprocessableEntity;

    public ListFasSchemesRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is null or "DRAFT" or "ACTIVE" or "RETIRED")
            .WithMessage("Status must be DRAFT, ACTIVE, or RETIRED.");
        RuleFor(x => x.Search).MaximumLength(255);
    }
}
