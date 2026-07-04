using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.Authentication.RecordAdminLogin;

internal sealed class RecordAdminLoginHandler(
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IAuditService audit,
    IAdminLoginRecorder loginRecorder,
    IClock clock) : ICommandHandler<RecordAdminLoginCommand>
{
    public async Task<Result> Handle(
        RecordAdminLoginCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated
            || currentUser.UserAccountId is null
            || currentUser.Portal != PortalAccessCodes.Admin)
        {
            return Result.Failure(IdentityErrors.AuthenticatedAdminRequired);
        }

        bool recorded = await loginRecorder.RecordSuccessfulLoginAsync(
            currentUser.UserAccountId.Value,
            clock.UtcNow.UtcDateTime,
            cancellationToken);

        if (!recorded)
        {
            return Result.Failure(IdentityErrors.UserAccountNotFound);
        }

        await RecordLoginAuditAsync(cancellationToken);
        return Result.Success();
    }

    private async Task RecordLoginAuditAsync(CancellationToken cancellationToken)
    {
        if (!adminAccess.IsSchoolAdmin || adminAccess.IsHqAdmin || currentUser.UserAccountId is not long userAccountId)
        {
            return;
        }

        foreach (long organizationId in adminAccess.ScopedOrganizationIds)
        {
            await audit.RecordSchoolActionAsync(
                new SchoolAuditContext(
                    AuditActionCodes.AdminLogin,
                    "UserAccount",
                    userAccountId,
                    organizationId,
                    new SchoolAuditDetails(
                        "Admin login",
                        RelatedIds: new Dictionary<string, long>
                        {
                            ["userAccountId"] = userAccountId
                        },
                        ReasonCode: "ADMIN_AUTH")),
                cancellationToken);
        }
    }
}
