using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.ReferenceData;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;

public sealed class EducationAccountReasonCodeGateway : IEducationAccountReasonCodeGateway
{
    public IReadOnlyList<ReferenceOption> GetCloseReasonOptions()
        =>
        [
            new(EducationAccountClosingReasonCodes.StudentIneligible, "Student ineligible"),
            new(EducationAccountClosingReasonCodes.DuplicateAccount, "Duplicate account"),
            new(EducationAccountClosingReasonCodes.AdminError, "Admin error"),
            new(EducationAccountClosingReasonCodes.Other, "Other")
        ];

    public IReadOnlyList<ReferenceOption> GetOpenReasonOptions() => [];
}
