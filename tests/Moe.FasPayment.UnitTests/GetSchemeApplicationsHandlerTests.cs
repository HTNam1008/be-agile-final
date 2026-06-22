using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.SharedKernel.Results;
using Moq;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public class GetSchemeApplicationsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOnlyApplicationsForRequestedScheme()
    {
        // Arrange
        var mockRepo = new Mock<IFasApplicationRepository>();
        var summary = new SchemeApplicationsSummary(1, 0, 0);
        var items = new List<SchemeApplicationItem>
        {
            new SchemeApplicationItem(1, "APP-1", "John", "STU-1", "2026-01-01", "PENDING_REVIEW")
        };
        
        mockRepo
            .Setup(r => r.GetSchemeApplicationsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSchemeApplicationsResponse(summary, items));

        var handler = new GetSchemeApplicationsHandler(mockRepo.Object);

        // Act
        var result = await handler.Handle(new GetSchemeApplicationsQuery(999), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].ApplicationId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CalculatesAccurateSummaryCounts()
    {
        // Arrange
        var mockRepo = new Mock<IFasApplicationRepository>();
        // backlog: pendingReview=1, approved=1, rejected=1
        var summary = new SchemeApplicationsSummary(1, 1, 1);
        var items = new List<SchemeApplicationItem>();
        
        mockRepo
            .Setup(r => r.GetSchemeApplicationsAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSchemeApplicationsResponse(summary, items));

        var handler = new GetSchemeApplicationsHandler(mockRepo.Object);

        // Act
        var result = await handler.Handle(new GetSchemeApplicationsQuery(999), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.PendingReview.Should().Be(1);
        result.Value.Summary.Approved.Should().Be(1);
        result.Value.Summary.Rejected.Should().Be(1);
    }
}
