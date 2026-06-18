using FluentAssertions;
using Moe.Modules.EducationAccountTopUp.Api.Admin;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.Filters;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertFixedRecipients;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests;

public sealed class UpsertFixedRecipientsValidationTests
{
    [Fact]
    public void Explicit_selection_accepts_unique_recipients_and_amount_overrides()
    {
        UpsertFixedRecipientsRequest request = new(
            TopUpAccountSelectionMode.ExplicitIds,
            Filter: null,
            Recipients:
            [
                new UpsertFixedRecipientDto(101, 25m),
                new UpsertFixedRecipientDto(102, null)
            ],
            ExcludedEducationAccountIds: []);

        var result = new UpsertFixedRecipientsRequestValidator().Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Explicit_selection_rejects_duplicate_recipient_ids()
    {
        UpsertFixedRecipientsRequest request = new(
            TopUpAccountSelectionMode.ExplicitIds,
            Filter: null,
            Recipients:
            [
                new UpsertFixedRecipientDto(101, 25m),
                new UpsertFixedRecipientDto(101, 30m)
            ],
            ExcludedEducationAccountIds: []);

        var result = new UpsertFixedRecipientsRequestValidator().Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Select_all_accepts_filter_and_bounded_exclusions()
    {
        UpsertFixedRecipientsRequest request = new(
            TopUpAccountSelectionMode.AllMatchingFilter,
            new TopUpAccountFilter(
                Search: null,
                OrganizationId: 10,
                SchoolingStatusCode: "ACTIVE",
                LevelCode: null,
                ClassCode: null,
                AccountStatusCode: "ACTIVE",
                AgeFrom: null,
                AgeTo: null,
                BalanceFrom: null,
                BalanceTo: null),
            Recipients: [],
            ExcludedEducationAccountIds: [101, 102]);

        var result = new UpsertFixedRecipientsRequestValidator().Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Select_all_rejects_explicit_recipients()
    {
        UpsertFixedRecipientsRequest request = new(
            TopUpAccountSelectionMode.AllMatchingFilter,
            new TopUpAccountFilter(null, 10, null, null, null, null, null, null, null, null),
            Recipients: [new UpsertFixedRecipientDto(101, null)],
            ExcludedEducationAccountIds: []);

        var result = new UpsertFixedRecipientsRequestValidator().Validate(request);

        result.IsValid.Should().BeFalse();
    }
}
