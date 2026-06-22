using System.Threading;
using System.Threading.Tasks;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.Applications.Reject;

internal sealed class RejectApplicationHandler(
    IFasApplicationRepository repository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<RejectApplicationCommand, RejectApplicationResponse>
{
    public async Task<Result<RejectApplicationResponse>> Handle(RejectApplicationCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RejectionReasonCode))
        {
            return Result<RejectApplicationResponse>.Failure(new Error("Validation.Error", "Rejection reason code is mandatory."));
        }

        var application = await repository.FindAsync(command.ApplicationId, cancellationToken);
        if (application == null)
        {
            return Result<RejectApplicationResponse>.Failure(new Error("Application.NotFound", "Application not found"));
        }

        string reviewerId = currentUser.UserAccountId?.ToString() ?? "system";

        application.Reject();

        var decision = FasApplicationReviewDecision.CreateRejection(application.Id, reviewerId, command.RejectionReasonCode, command.Remarks);
        await repository.AddDecisionAsync(decision, cancellationToken);
        
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<RejectApplicationResponse>.Success(
            new RejectApplicationResponse(application.Id, application.StatusCode, decision.Decision, command.RejectionReasonCode)
        );
    }
}
