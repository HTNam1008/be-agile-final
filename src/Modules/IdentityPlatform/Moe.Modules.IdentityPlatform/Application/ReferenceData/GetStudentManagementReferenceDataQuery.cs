using Moe.Application.Abstractions.Messaging;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.ReferenceData;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.ReferenceData;

public sealed record GetStudentManagementReferenceDataQuery()
    : IQuery<StudentManagementReferenceDataResponse>;

public sealed record StudentManagementReferenceDataResponse(
    StudentListFilterReferenceData StudentListFilters,
    StudentProfileReferenceData StudentProfile,
    EducationAccountReferenceData EducationAccount);

public sealed record StudentListFilterReferenceData(
    IReadOnlyList<ReferenceOption> AccountStatuses,
    IReadOnlyList<ReferenceOption> PortalAccessStatuses,
    IReadOnlyList<ReferenceOption> EnrollmentStatuses,
    IReadOnlyList<ReferenceOption> Levels);

public sealed record StudentProfileReferenceData(
    IReadOnlyList<ReferenceOption> Levels);

public sealed record EducationAccountReferenceData(
    IReadOnlyList<ReferenceOption> CloseReasons,
    IReadOnlyList<ReferenceOption> OpenReasons);

internal sealed class GetStudentManagementReferenceDataHandler(
    IEducationAccountReasonCodeGateway educationAccountReasons)
    : IQueryHandler<GetStudentManagementReferenceDataQuery, StudentManagementReferenceDataResponse>
{
    public Task<Result<StudentManagementReferenceDataResponse>> Handle(
        GetStudentManagementReferenceDataQuery query,
        CancellationToken cancellationToken)
    {
        StudentManagementReferenceDataResponse response = new(
            new StudentListFilterReferenceData(
                AccountStatuses:
                [
                    Option(nameof(AdminStudentAccountStatusFilter.Active), "Active"),
                    Option(nameof(AdminStudentAccountStatusFilter.PendingClosure), "Pending closure"),
                    Option(nameof(AdminStudentAccountStatusFilter.Closed), "Closed"),
                    Option(nameof(AdminStudentAccountStatusFilter.NoAccount), "No account")
                ],
                PortalAccessStatuses:
                [
                    Option(nameof(AdminStudentPortalAccessStatusFilter.Active), "Active"),
                    Option(nameof(AdminStudentPortalAccessStatusFilter.Disabled), "Disabled")
                ],
                EnrollmentStatuses:
                [
                    Option(nameof(AdminStudentEnrollmentStatusFilter.Enrolled), "Enrolled"),
                    Option(nameof(AdminStudentEnrollmentStatusFilter.NotEnrolled), "Not enrolled")
                ],
                Levels: SchoolLevelCodes.All.Select(ToLevelOption).ToArray()),
            new StudentProfileReferenceData(
                Levels: SchoolLevelCodes.All.Select(ToLevelOption).ToArray()),
            new EducationAccountReferenceData(
                educationAccountReasons.GetCloseReasonOptions(),
                educationAccountReasons.GetOpenReasonOptions()));

        return Task.FromResult(Result<StudentManagementReferenceDataResponse>.Success(response));
    }

    private static ReferenceOption Option(string value, string label) => new(value, label);

    private static ReferenceOption ToLevelOption(string value)
        => value switch
        {
            SchoolLevelCodes.PostSecondary => Option(value, "Post-Secondary"),
            SchoolLevelCodes.Bachelor => Option(value, "Bachelor"),
            SchoolLevelCodes.Master => Option(value, "Master"),
            SchoolLevelCodes.Phd => Option(value, "Doctor"),
            _ => Option(value, value)
        };
}
