using System;
using FluentAssertions;
using Moe.Modules.FasPayment.Domain.Fas;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public class FasApplicationTests
{
    [Fact]
    public void SubmitDraft_ShouldSetStatusToSubmitted()
    {
        // Act
        var application = SubmittedApplication();

        // Assert
        application.StatusCode.Should().Be(FasApplicationStatuses.Submitted);
    }

    [Fact]
    public void SubmitDraft_ShouldUseSingaporeBusinessDateForSubmittedDate()
    {
        DateTime utcNow = new(2026, 6, 30, 16, 30, 0, DateTimeKind.Utc);
        var application = DraftApplication(utcNow);

        application.SubmitDraft(1, utcNow);

        application.SubmittedDate.Should().Be(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public void Approve_WhenSubmitted_ShouldSetStatusToApproved()
    {
        // Arrange
        var application = SubmittedApplication();

        // Act
        application.Approve(1, DateTime.UtcNow);

        // Assert
        application.StatusCode.Should().Be("APPROVED");
        application.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Reject_WhenSubmitted_ShouldSetStatusToRejected()
    {
        // Arrange
        var application = SubmittedApplication();

        // Act
        application.Reject(1, DateTime.UtcNow);

        // Assert
        application.StatusCode.Should().Be("REJECTED");
        application.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_ShouldThrowException()
    {
        // Arrange
        var application = SubmittedApplication();
        application.Approve(1, DateTime.UtcNow);

        // Act
        Action act = () => application.Approve(1, DateTime.UtcNow);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reject_WhenAlreadyApproved_ShouldThrowException()
    {
        // Arrange
        var application = SubmittedApplication();
        application.Approve(1, DateTime.UtcNow);

        // Act
        Action act = () => application.Reject(1, DateTime.UtcNow);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reject_WhenAlreadyRejected_ShouldThrowException()
    {
        // Arrange
        var application = SubmittedApplication();
        application.Reject(1, DateTime.UtcNow);

        // Act
        Action act = () => application.Reject(1, DateTime.UtcNow);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Approve_WhenAlreadyRejected_ShouldThrowException()
    {
        // Arrange
        var application = SubmittedApplication();
        application.Reject(1, DateTime.UtcNow);

        // Act
        Action act = () => application.Approve(1, DateTime.UtcNow);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CreateRejection_NullReason_ShouldThrow()
    {
        Action act = () => FasApplicationReviewDecision.CreateRejection(1, 1L, null!, null, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateRejection_EmptyReason_ShouldThrow()
    {
        Action act = () => FasApplicationReviewDecision.CreateRejection(1, 1L, "", null, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    private static FasApplication SubmittedApplication()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var application = DraftApplication(now);

        application.SubmitDraft(1, now);
        return application;
    }

    private static FasApplication DraftApplication(DateTime now)
    {
        var application = FasApplication.CreateDraft(
            "APP-001",
            1,
            1,
            "STU-001",
            "John Doe",
            "S****001A",
            new DateOnly(2006, 1, 1),
            "Singaporean",
            "90000000",
            "1 Test Road",
            "john.doe@example.test",
            1,
            "Test School",
            "PERSONAL_ACCOUNT",
            1,
            now);

        return application;
    }
}
