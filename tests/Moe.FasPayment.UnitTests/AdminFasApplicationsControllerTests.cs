using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.FasPayment.Api.Admin;
using Moe.Modules.FasPayment.Application.Applications.Approve;
using Moe.Modules.FasPayment.Application.Applications.Reject;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.SharedKernel.Results;
using Moq;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public class AdminFasApplicationsControllerTests
{
    private readonly Mock<ICommandDispatcher> _commandsMock;
    private readonly Mock<IQueryDispatcher> _queriesMock;
    private readonly AdminFasApplicationsController _controller;

    public AdminFasApplicationsControllerTests()
    {
        _commandsMock = new Mock<ICommandDispatcher>();
        _queriesMock = new Mock<IQueryDispatcher>();
        _controller = new AdminFasApplicationsController(_commandsMock.Object, _queriesMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task ApproveEndpoint_SecondApproveOnAlreadyApproved_Returns422()
    {
        // Arrange
        _commandsMock
            .Setup(c => c.Send(It.IsAny<ApproveApplicationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("Cannot approve application with status APPROVED. Must be PENDING_REVIEW."));

        // Act
        var result = await _controller.ApproveApplication(1, new ApproveApplicationRequest(), CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task RejectEndpoint_MissingRejectionReasonCode_Returns422()
    {
        // Arrange
        _commandsMock
            .Setup(c => c.Send(It.IsAny<RejectApplicationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RejectApplicationResponse>.Failure(new Error("Validation.Error", "Rejection reason code is mandatory.")));

        // Act
        var result = await _controller.RejectApplication(1, new RejectApplicationRequest { RejectionReasonCode = null! }, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task RejectEndpoint_EmptyRejectionReasonCode_Returns422()
    {
        // Arrange
        _commandsMock
            .Setup(c => c.Send(It.IsAny<RejectApplicationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RejectApplicationResponse>.Failure(new Error("Validation.Error", "Rejection reason code is mandatory.")));

        // Act
        var result = await _controller.RejectApplication(1, new RejectApplicationRequest { RejectionReasonCode = "  " }, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(422);
    }
}
