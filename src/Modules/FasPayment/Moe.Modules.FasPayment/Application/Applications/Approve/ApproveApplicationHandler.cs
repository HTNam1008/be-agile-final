using System.Threading;
using System.Threading.Tasks;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.FasPayment.Application.Notifications;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.Applications.Approve;

internal sealed class ApproveApplicationHandler(
    IFasApplicationRepository repository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    FasEmailNotificationService fasEmails,
    FasInAppNotificationService fasNotifications,
    IClock clock) : ICommandHandler<ApproveApplicationCommand, ApproveApplicationResponse>
{
    public async Task<Result<ApproveApplicationResponse>> Handle(ApproveApplicationCommand command, CancellationToken cancellationToken)
    {
        var application = await repository.FindAsync(command.ApplicationId, cancellationToken);
        if (application == null)
        {
            return Result<ApproveApplicationResponse>.Failure(new Error("Application.NotFound", "Application not found"));
        }

        long reviewerId = currentUser?.UserAccountId ?? 1111;

        DateTime now = clock.UtcNow.UtcDateTime;
        application.Approve(reviewerId, now);

        var decision = FasApplicationReviewDecision.CreateApproval(application.Id, reviewerId, command.Remarks, now);
        await repository.AddDecisionAsync(decision, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await fasEmails.SendApplicationApprovedAsync(application.Id, cancellationToken);
        await fasNotifications.SendApplicationApprovedAsync(application.Id, cancellationToken);

        return Result<ApproveApplicationResponse>.Success(
            new ApproveApplicationResponse(application.Id, application.StatusCode, decision.Decision)
        );
    }
}
