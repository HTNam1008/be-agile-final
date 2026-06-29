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
        RuleFor(x => x.SchemeCode).NotEmpty().MaximumLength(50).Must(BeTrimmed).WithMessage("Scheme code cannot contain leading or trailing spaces.");
        RuleFor(x => x.GrantCode).MaximumLength(100).Must(BeTrimmedOrEmpty).WithMessage("Grant code cannot contain leading or trailing spaces.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255).Must(BeTrimmed).WithMessage("Name cannot contain leading or trailing spaces.");
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly)).GreaterThanOrEqualTo(_ => Today()).WithMessage("Start date cannot be before today.");
        RuleFor(x => x.EndDate).NotEqual(default(DateOnly));
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        RuleFor(x => x.CourseIds).NotNull();
        RuleFor(x => x.CourseIds).Must(x => x is not null && x.Count <= 500).WithMessage("A scheme can target at most 500 courses.");
        RuleFor(x => x.CourseIds).Must(x => x is not null && x.All(id => id > 0) && x.Distinct().Count() == x.Count).WithMessage("Course IDs must be positive and unique.");
        RuleFor(x => x.SubsidyType).Must(x => x is "FIXED" or "PERCENTAGE");
        RuleFor(x => x.CriteriaTemplate).NotNull().NotEmpty();
        RuleFor(x => x.CriteriaTemplate).Must(x => x is not null && x.Count <= 10).WithMessage("A scheme can have at most 10 criteria.");
        RuleFor(x => x.Tiers).NotNull().NotEmpty();
        RuleFor(x => x.Tiers).Must(x => x is not null && x.Count <= 20).WithMessage("A scheme can have at most 20 tiers.");
        RuleFor(x => x).Custom(ValidateStructure);
    }

    private static void ValidateStructure(CreateFasSchemeRequest request, ValidationContext<CreateFasSchemeRequest> context)
    {
        if (request.CriteriaTemplate is null || request.Tiers is null) return;
        if (request.CriteriaTemplate.Any(item => item is null))
        {
            context.AddFailure("CriteriaTemplate", "Criteria template entries cannot be null.");
            return;
        }
        ValidateOrders(request.CriteriaTemplate.Select(x => x.DisplayOrder), "criteriaTemplate", context);
        ValidateOrders(request.Tiers.Where(x => x is not null).Select(x => x.DisplayOrder), "tiers", context);
        var template = request.CriteriaTemplate.OrderBy(x => x.DisplayOrder).ToArray();
        var duplicateCriteriaTypes = template.GroupBy(x => x.CriteriaType).Where(x => x.Count() > 1).Select(x => x.Key).ToArray();
        if (duplicateCriteriaTypes.Length > 0) context.AddFailure("CriteriaTemplate", $"Criteria types must be unique: {string.Join(", ", duplicateCriteriaTypes)}.");
        var duplicateTierLabels = request.Tiers.Where(x => x is not null).GroupBy(x => x!.Label?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1).Select(x => x.Key).ToArray();
        if (duplicateTierLabels.Length > 0) context.AddFailure("Tiers", $"Tier labels must be unique: {string.Join(", ", duplicateTierLabels)}.");
        for (int i = 0; i < template.Length; i++)
        {
            FasCriteriaTemplateItem item = template[i];
            if (item.CriteriaType is not ("AGE" or "GDP" or "GHI" or "PCI" or "NATIONALITY" or "PARENT_NATIONALITY" or "ACCOUNT_TYPE")) context.AddFailure("CriteriaTemplate", $"Unsupported criteria type '{item.CriteriaType}'.");
            bool last = i == template.Length - 1;
            if (last ? item.ConnectorToNext is not null : item.ConnectorToNext is not ("AND" or "OR")) context.AddFailure("CriteriaTemplate", "Only the final connector may be null; earlier connectors must be AND or OR.");
        }
        foreach (CreateFasTierRequest? tier in request.Tiers)
        {
            if (tier is null)
            {
                context.AddFailure("Tiers", "Tier entries cannot be null.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(tier.Label) || tier.Label.Length > 255) context.AddFailure("Tiers", "Each tier requires a label of at most 255 characters.");
            if (request.SubsidyType == "PERCENTAGE" && (tier.SubsidyValue < 1 || tier.SubsidyValue > 100)) context.AddFailure("Tiers", "Percentage subsidy value must be from 1 to 100.");
            else if (request.SubsidyType == "FIXED" && tier.SubsidyValue < 0) context.AddFailure("Tiers", "Fixed subsidy value cannot be below 0.");
            if (tier.GrantCode is not null || tier.SubsidyType is not null) context.AddFailure("Tiers", "grantCode and subsidyType are scheme-level fields.");
            if (tier.CriteriaValues is null)
            {
                context.AddFailure("Tiers", "Each tier must provide criteria values.");
                continue;
            }
            if (tier.CriteriaValues.Any(value => value is null))
            {
                context.AddFailure("Tiers", "Criteria values cannot contain null entries.");
                continue;
            }
            if (tier.CriteriaValues.Count != template.Length || tier.CriteriaValues.Select(x => x.DisplayOrder).Distinct().Count() != template.Length) context.AddFailure("Tiers", "Each tier must provide every template criterion exactly once.");
            foreach (FasCriteriaTemplateItem item in template)
            {
                FasTierCriteriaValue[] matches = tier.CriteriaValues.Where(x => x.DisplayOrder == item.DisplayOrder).ToArray();
                if (matches.Length != 1)
                {
                    context.AddFailure("Tiers", $"Tier must provide criteria display order {item.DisplayOrder} exactly once.");
                    continue;
                }
                FasTierCriteriaValue value = matches[0];
                if (item.CriteriaType is "NATIONALITY" or "PARENT_NATIONALITY")
                {
                    if (value.NumberFrom.HasValue || value.NumberTo.HasValue || value.Nationalities is null || value.Nationalities.Count == 0 || value.Nationalities.Any(x => !Moe.Modules.FasPayment.Domain.Fas.FasNationalities.All.Contains(x))) context.AddFailure("Tiers", "Nationality criteria require at least one supported nationality and no numeric bounds.");
                }
                else if (item.CriteriaType == "ACCOUNT_TYPE")
                {
                    if (value.NumberFrom.HasValue || value.NumberTo.HasValue || value.Nationalities is null || value.Nationalities.Count == 0 || value.Nationalities.Any(x => x is not ("EDUCATION_ACCOUNT" or "PERSONAL_ACCOUNT"))) context.AddFailure("Tiers", "Account type requires Education Account and/or Personal Account and no numeric bounds.");
                }
                else if (!value.NumberFrom.HasValue || !value.NumberTo.HasValue || value.NumberFrom > value.NumberTo || value.Nationalities?.Count > 0) context.AddFailure("Tiers", "Numeric criteria require a valid range and no nationalities.");
                else if (value.NumberFrom < 0 || value.NumberTo < 0) context.AddFailure("Tiers", $"{item.CriteriaType} ranges cannot be negative.");
                else if (item.CriteriaType == "AGE" && value.NumberTo > 120) context.AddFailure("Tiers", "Age ranges cannot exceed 120.");
            }
        }
    }

    private static void ValidateOrders(IEnumerable<int> orders, string name, ValidationContext<CreateFasSchemeRequest> context)
    {
        int[] values = orders.Order().ToArray();
        if (!values.SequenceEqual(Enumerable.Range(1, values.Length))) context.AddFailure(name, "Display orders must be unique and contiguous from 1.");
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
    private static bool BeTrimmed(string? value) => value is not null && value == value.Trim();
    private static bool BeTrimmedOrEmpty(string? value) => value is null || value == value.Trim();
}

internal sealed class ListFasSchemesRequestValidator : AbstractValidator<ListFasSchemesRequest>, IValidationFailureStatusCodeProvider
{
    public int ValidationFailureStatusCode => ApiResponseCodes.UnprocessableEntity;

    public ListFasSchemesRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is null or "DRAFT" or "ACTIVE" or "NOT_STARTED" or "RETIRED" or "DISABLED" or "CLOSED")
            .WithMessage("Status must be DRAFT, ACTIVE, NOT_STARTED, RETIRED, DISABLED, or CLOSED.");
        RuleFor(x => x.Search).MaximumLength(255);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
