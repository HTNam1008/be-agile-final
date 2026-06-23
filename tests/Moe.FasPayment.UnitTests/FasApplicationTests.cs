using System;
using FluentAssertions;
using Moe.Modules.FasPayment.Domain.Fas;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public class FasApplicationTests
{
    [Fact]
    public void Submit_ShouldSetStatusToPendingReview()
    {
        // Act
        var application = FasApplication.Submit("APP-001", 1, "STU-001", "John Doe", new DateOnly(2026, 1, 1));

        // Assert
        application.StatusCode.Should().Be("PENDING_REVIEW");
    }

    [Fact]
    public void Approve_WhenPendingReview_ShouldSetStatusToApproved()
    {
        // Arrange
        var application = FasApplication.Submit("APP-001", 1, "STU-001", "John Doe", new DateOnly(2026, 1, 1));

        // Act
        application.Approve();

        // Assert
        application.StatusCode.Should().Be("APPROVED");
        application.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Reject_WhenPendingReview_ShouldSetStatusToRejected()
    {
        // Arrange
        var application = FasApplication.Submit("APP-001", 1, "STU-001", "John Doe", new DateOnly(2026, 1, 1));

        // Act
        application.Reject();

        // Assert
        application.StatusCode.Should().Be("REJECTED");
        application.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_ShouldThrowException()
    {
        // Arrange
        var application = FasApplication.Submit("APP-001", 1, "STU-001", "John Doe", new DateOnly(2026, 1, 1));
        application.Approve();

        // Act
        Action act = () => application.Approve();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reject_WhenAlreadyApproved_ShouldThrowException()
    {
        // Arrange
        var application = FasApplication.Submit("APP-001", 1, "STU-001", "John Doe", new DateOnly(2026, 1, 1));
        application.Approve();

        // Act
        Action act = () => application.Reject();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Reject_WhenAlreadyRejected_ShouldThrowException()
    {
        // Arrange
        var application = FasApplication.Submit("APP-001", 1, "STU-001", "John Doe", new DateOnly(2026, 1, 1));
        application.Reject();

        // Act
        Action act = () => application.Reject();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Approve_WhenAlreadyRejected_ShouldThrowException()
    {
        // Arrange
        var application = FasApplication.Submit("APP-001", 1, "STU-001", "John Doe", new DateOnly(2026, 1, 1));
        application.Reject();

        // Act
        Action act = () => application.Approve();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CreateRejection_NullReason_ShouldThrow()
    {
        Action act = () => FasApplicationReviewDecision.CreateRejection(1, 1111, null!, null, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateRejection_EmptyReason_ShouldThrow()
    {
        Action act = () => FasApplicationReviewDecision.CreateRejection(1, 1111, "", null, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }
}
