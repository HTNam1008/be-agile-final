using FluentAssertions;
using Moe.Modules.FasPayment.Application.StudentApplications;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class AdminFasApplicationStatusPolicyTests
{
    [Theory]
    [InlineData("SUBMITTED", "PENDING", "PENDING")]
    [InlineData("PENDING_REVIEW", "PENDING", "PENDING")]
    [InlineData("APPROVED", "APPROVED", "APPROVED")]
    [InlineData("REJECTED", "REJECTED", "REJECTED")]
    public void Visible_review_statuses_are_included(string applicationStatus, string selectionStatus, string expected)
    {
        AdminFasApplicationStatusPolicy.TryGetReviewStatus(applicationStatus, selectionStatus, out string? status)
            .Should().BeTrue();
        status.Should().Be(expected);
    }

    [Theory]
    [InlineData("DRAFT", "DRAFT")]
    [InlineData("WITHDRAWN", "CANCELLED")]
    [InlineData("APPROVED", "REDEEMED")]
    [InlineData("APPROVED", "EXPIRED")]
    public void Non_review_statuses_are_excluded(string applicationStatus, string selectionStatus)
    {
        AdminFasApplicationStatusPolicy.TryGetReviewStatus(applicationStatus, selectionStatus, out string? status)
            .Should().BeFalse();
        status.Should().BeNull();
    }
}
