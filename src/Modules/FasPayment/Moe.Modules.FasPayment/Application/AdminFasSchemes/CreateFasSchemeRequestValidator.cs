using FluentValidation;
using Moe.Application.Abstractions.Clock;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Validation;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;

namespace Moe.Modules.FasPayment.Application.AdminFasSchemes;

internal sealed class CreateFasSchemeRequestValidator : AbstractValidator<CreateFasSchemeRequest>, IValidationFailureStatusCodeProvider
{
    private readonly IClock _clock;

    public int ValidationFailureStatusCode => ApiResponseCodes.UnprocessableEntity;

    public CreateFasSchemeRequestValidator(IClock clock)
    {
        _clock = clock;
        RuleFor(x => x.SchemeCode).NotEmpty().MaximumLength(50).Must(BeTrimmed).WithMessage("Scheme code cannot contain leading or trailing spaces.");
        RuleFor(x => x.GrantCode).MaximumLength(100).Must(BeTrimmedOrEmpty).WithMessage("Grant code cannot contain leading or trailing spaces.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255).Must(BeTrimmed).WithMessage("Name cannot contain leading or trailing spaces.");
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly)).GreaterThanOrEqualTo(_ => Today()).WithMessage("Start date cannot be before today.");
        RuleFor(x => x.EndDate).NotEqual(default(DateOnly));
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("End date must be after start date.");
        RuleFor(x => x.CourseIds).NotNull().NotEmpty().WithMessage("Select at least one eligible course.");
        RuleFor(x => x.CourseIds).Must(x => x is not null && x.Count <= 500).WithMessage("A scheme can target at most 500 courses.");
        RuleFor(x => x.CourseIds).Must(x => x is not null && x.All(id => id > 0) && x.Distinct().Count() == x.Count).WithMessage("Course IDs must be positive and unique.");
        RuleFor(x => x.SubsidyType).Must(x => x is "FIXED" or "PERCENTAGE");
        RuleFor(x => x).Must(HasCriteria).WithMessage("A scheme requires at least one criterion.");
        RuleFor(x => x.CriteriaTemplate).Must(x => x is null || x.Count <= 10).WithMessage("A scheme can have at most 10 criteria.");
        RuleFor(x => x.CriteriaGroups).Must(x => x is null || x.Count <= 10).WithMessage("A scheme can have at most 10 criteria groups.");
        RuleFor(x => x.Tiers).NotNull().NotEmpty();
        RuleFor(x => x.Tiers).Must(x => x is not null && x.Count <= 20).WithMessage("A scheme can have at most 20 tiers.");
        RuleFor(x => x).Custom(ValidateStructure);
    }

    private static void ValidateStructure(CreateFasSchemeRequest request, ValidationContext<CreateFasSchemeRequest> context)
    {
        if (request.Tiers is null) return;
        if (request.CriteriaTemplate is not null && request.CriteriaTemplate.Any(item => item is null))
        {
            context.AddFailure("CriteriaTemplate", "Criteria template entries cannot be null.");
            return;
        }
        if (request.CriteriaGroups is not null && request.CriteriaGroups.Any(group => group is null || group.Criteria is null || group.Criteria.Any(item => item is null)))
        {
            context.AddFailure("CriteriaGroups", "Criteria groups and criteria entries cannot be null.");
            return;
        }

        if (request.CriteriaGroups is null or { Count: 0 })
        {
            ValidateLegacyConnectorShape(request.CriteriaTemplate, context);
        }

        IReadOnlyList<FasCriteriaGroupRequest> groups = FasCriteriaGroupNormalizer.Normalize(request);
        ValidateGroups(groups, context);
        FasCriteriaTemplateItem[] template = FasCriteriaGroupNormalizer.Flatten(groups).OrderBy(x => x.DisplayOrder).ToArray();
        if (template.Length > 10) context.AddFailure("CriteriaTemplate", "A scheme can have at most 10 criteria.");
        ValidateOrders(template.Select(x => x.DisplayOrder), "criteriaTemplate", context);
        ValidateRequiredCriteria(template, context);
        ValidateOrders(request.Tiers.Where(x => x is not null).Select(x => x.DisplayOrder), "tiers", context);
        foreach (FasCriteriaGroupRequest group in groups)
        {
            var duplicateCriteriaTypes = group.Criteria.GroupBy(x => x.CriteriaType).Where(x => x.Count() > 1).Select(x => x.Key).ToArray();
            if (duplicateCriteriaTypes.Length > 0) context.AddFailure("CriteriaGroups", $"Criteria types must be unique within a group: {string.Join(", ", duplicateCriteriaTypes)}.");
        }
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
                else if (item.CriteriaType == "AGE" && (value.NumberFrom < 16 || value.NumberTo > 30)) context.AddFailure("Tiers", "Age ranges must be within 16 to 30.");
            }
        }
    }

    private static void ValidateOrders(IEnumerable<int> orders, string name, ValidationContext<CreateFasSchemeRequest> context)
    {
        int[] values = orders.Order().ToArray();
        if (!values.SequenceEqual(Enumerable.Range(1, values.Length))) context.AddFailure(name, "Display orders must be unique and contiguous from 1.");
    }

    private static bool HasCriteria(CreateFasSchemeRequest request)
        => request.CriteriaGroups?.Any(group => group.Criteria?.Count > 0) == true ||
           request.CriteriaTemplate?.Count > 0;

    private static void ValidateGroups(IReadOnlyList<FasCriteriaGroupRequest> groups, ValidationContext<CreateFasSchemeRequest> context)
    {
        if (groups.Count == 0)
        {
            context.AddFailure("CriteriaGroups", "A scheme requires at least one criteria group.");
            return;
        }

        ValidateOrders(groups.Select(group => group.DisplayOrder), "criteriaGroups", context);

        foreach (FasCriteriaGroupRequest group in groups)
        {
            if (group.Criteria.Count == 0)
            {
                context.AddFailure("CriteriaGroups", $"Criteria group {group.DisplayOrder} requires at least one criterion.");
                continue;
            }

            string[] invalidConnectors = group.Criteria
                .Where(item => item.ConnectorToNext is not (null or "AND"))
                .Select(item => item.ConnectorToNext!)
                .Distinct()
                .ToArray();

            if (invalidConnectors.Length > 0)
            {
                context.AddFailure("CriteriaGroups", "Criteria inside a group may only use AND. Groups are combined with OR.");
            }
        }
    }

    private static void ValidateLegacyConnectorShape(IReadOnlyList<FasCriteriaTemplateItem>? template, ValidationContext<CreateFasSchemeRequest> context)
    {
        if (template is null || template.Count == 0)
        {
            return;
        }

        FasCriteriaTemplateItem[] ordered = template.OrderBy(x => x.DisplayOrder).ToArray();
        for (int i = 0; i < ordered.Length; i++)
        {
            bool last = i == ordered.Length - 1;
            if (last ? ordered[i].ConnectorToNext is not null : ordered[i].ConnectorToNext is not ("AND" or "OR"))
            {
                context.AddFailure("CriteriaTemplate", "Only the final connector may be null; earlier connectors must be AND or OR.");
            }
        }
    }

    private static void ValidateRequiredCriteria(IReadOnlyList<FasCriteriaTemplateItem> template, ValidationContext<CreateFasSchemeRequest> context)
    {
        string[] requiredCriteria = ["GHI", "PCI"];
        string[] missing = requiredCriteria
            .Where(required => template.All(item => item.CriteriaType != required))
            .ToArray();

        if (missing.Length > 0)
        {
            context.AddFailure("CriteriaTemplate", $"Criteria template must include: {string.Join(", ", missing)}.");
        }
    }

    private DateOnly Today() => _clock.TodayInSingapore();
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
        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) || sortBy is "createdDate" or "schemeName" or "schemeCode" or "duration" or "status" or "applicationCount")
            .WithMessage("SortBy must be createdDate, schemeName, schemeCode, duration, status, or applicationCount.");
        RuleFor(x => x.SortDirection)
            .Must(direction => string.IsNullOrWhiteSpace(direction) || direction is "asc" or "desc")
            .WithMessage("SortDirection must be asc or desc.");
        RuleFor(x => x)
            .Must(x => !x.DurationFrom.HasValue || !x.DurationTo.HasValue || x.DurationFrom.Value <= x.DurationTo.Value)
            .WithMessage("Duration from must be on or before duration to.");
    }
}
