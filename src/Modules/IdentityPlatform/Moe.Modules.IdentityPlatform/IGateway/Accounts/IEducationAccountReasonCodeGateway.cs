using Moe.Modules.IdentityPlatform.IGateway.ReferenceData;

namespace Moe.Modules.IdentityPlatform.IGateway.Accounts;

public interface IEducationAccountReasonCodeGateway
{
    IReadOnlyList<ReferenceOption> GetCloseReasonOptions();
    IReadOnlyList<ReferenceOption> GetOpenReasonOptions();
}
