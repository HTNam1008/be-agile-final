using FluentAssertions;
using System.Text.Json;
using Moe.Modules.IdentityPlatform.Application.ReferenceData;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.ReferenceData;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Application.ReferenceData;

public sealed class GetStudentManagementReferenceDataHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsStudentManagementOptionsWithoutAllPseudoValues()
    {
        FakeEducationAccountReasonCodeGateway gateway = new(
            closeReasons: [new ReferenceOption("CLOSE_FROM_GATEWAY", "Close from gateway")],
            openReasons: []);
        GetStudentManagementReferenceDataHandler handler = new(gateway);

        var result = await handler.Handle(new GetStudentManagementReferenceDataQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        StudentManagementReferenceDataResponse response = result.Value;

        response.StudentListFilters.AccountStatuses.Select(x => x.Value)
            .Should().Equal(
                nameof(AdminStudentAccountStatusFilter.Active),
                nameof(AdminStudentAccountStatusFilter.PendingClosure),
                nameof(AdminStudentAccountStatusFilter.Closed),
                nameof(AdminStudentAccountStatusFilter.NoAccount));

        response.StudentListFilters.EnrollmentStatuses.Select(x => x.Value)
            .Should().Equal(
                nameof(AdminStudentEnrollmentStatusFilter.Enrolled),
                nameof(AdminStudentEnrollmentStatusFilter.NotEnrolled));

        response.StudentListFilters.Levels.Select(x => x.Value)
            .Should().Equal(
                "PRI_1", "PRI_2", "PRI_3", "PRI_4", "PRI_5", "PRI_6",
                "SEC_1", "SEC_2", "SEC_3", "SEC_4", "SEC_5");
        response.StudentListFilters.Levels.Select(x => x.Value)
            .Should().NotContain(["UNI_Y1", "UNI_Y2", "UNI_Y3", "UNI_Y4"]);

        string serialized = JsonSerializer.Serialize(response);
        serialized.Should().NotContain("ResidencyFilters");
        serialized.Should().NotContain("ResidencyCodes");

        response.EducationAccount.CloseReasons
            .Should().Equal([new ReferenceOption("CLOSE_FROM_GATEWAY", "Close from gateway")]);
        response.EducationAccount.OpenReasons.Should().BeEmpty();
        gateway.GetCloseReasonOptionsCalls.Should().Be(1);
        gateway.GetOpenReasonOptionsCalls.Should().Be(1);
    }

    private sealed class FakeEducationAccountReasonCodeGateway(
        IReadOnlyList<ReferenceOption> closeReasons,
        IReadOnlyList<ReferenceOption> openReasons) : IEducationAccountReasonCodeGateway
    {
        public int GetCloseReasonOptionsCalls { get; private set; }
        public int GetOpenReasonOptionsCalls { get; private set; }

        public IReadOnlyList<ReferenceOption> GetCloseReasonOptions()
        {
            GetCloseReasonOptionsCalls++;
            return closeReasons;
        }

        public IReadOnlyList<ReferenceOption> GetOpenReasonOptions()
        {
            GetOpenReasonOptionsCalls++;
            return openReasons;
        }
    }
}
