using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Application.Dashboard;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Dashboard.GetStudentDashboard;

internal sealed class GetStudentDashboardSummaryHandler(
    ICurrentUser currentUser,
    IStudentDirectory students,
    IEducationAccountDirectory educationAccounts,
    IEducationAccountPaymentGateway educationAccountPayments,
    IStudentDashboardCourseRepository dashboardCourses)
    : IQueryHandler<GetStudentDashboardSummaryQuery, StudentDashboardSummaryResponse>
{
    private const string StudentRole = "STUDENT";

    public async Task<Result<StudentDashboardSummaryResponse>> Handle(
        GetStudentDashboardSummaryQuery query,
        CancellationToken cancellationToken)
    {
        if (!IsAuthenticatedEServiceStudent())
        {
            return Result<StudentDashboardSummaryResponse>.Failure(DashboardErrors.AuthenticatedStudentRequired);
        }

        long personId = currentUser.PersonId!.Value;

        StudentSummary? student = await students.FindByPersonIdAsync(personId, cancellationToken);
        if (student is null)
        {
            return Result<StudentDashboardSummaryResponse>.Failure(DashboardErrors.StudentNotFound);
        }

        EducationAccountSummary? account = await educationAccounts.FindByPersonIdAsync(personId, cancellationToken);
        if (account is null)
        {
            return Result<StudentDashboardSummaryResponse>.Failure(DashboardErrors.EducationAccountNotFound);
        }

        EducationAccountPaymentBalance? paymentBalance =
            await educationAccountPayments.GetAvailableBalanceAsync(personId, cancellationToken);
        decimal currentBalance = paymentBalance?.CurrentBalance ?? account.CurrentBalance;
        decimal reservedAmount = paymentBalance?.HeldBalance ?? 0m;
        decimal availableBalance = paymentBalance?.AvailableBalance ?? currentBalance;
        int currentCourseCount = await dashboardCourses.CountCurrentCoursesAsync(personId, cancellationToken);

        return Result<StudentDashboardSummaryResponse>.Success(new StudentDashboardSummaryResponse(
            new StudentDashboardProfileResponse(
                student.PersonId,
                student.DisplayName,
                GetStudentDashboardHandler.ToGreetingName(student.DisplayName),
                student.SchoolName),
            new StudentDashboardEducationAccountResponse(
                account.EducationAccountId,
                account.AccountNumber,
                account.CurrencyCode,
                account.AccountStatusCode,
                GetStudentDashboardHandler.ToAccountStatusLabel(account.AccountStatusCode),
                currentBalance,
                GetStudentDashboardHandler.ToCurrencyDisplay(account.CurrencyCode, currentBalance),
                reservedAmount,
                GetStudentDashboardHandler.ToCurrencyDisplay(account.CurrencyCode, reservedAmount),
                availableBalance,
                GetStudentDashboardHandler.ToCurrencyDisplay(account.CurrencyCode, availableBalance)),
            currentCourseCount));
    }

    private bool IsAuthenticatedEServiceStudent()
    {
        return currentUser.IsAuthenticated
            && currentUser.PersonId is not null
            && currentUser.Portal == PortalCodes.EService
            && currentUser.Roles.Contains(StudentRole);
    }
}

