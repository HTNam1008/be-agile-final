using System.Threading;
using System.Threading.Tasks;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.Applications.Approve;

internal sealed class ApproveApplicationHandler(
    IFasApplicationRepository repository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<ApproveApplicationCommand, ApproveApplicationResponse>
{
    public async Task<Result<ApproveApplicationResponse>> Handle(ApproveApplicationCommand command, CancellationToken cancellationToken)
    {
        var application = await repository.FindAsync(command.ApplicationId, cancellationToken);
        if (application == null)
        {
            return Result<ApproveApplicationResponse>.Failure(new Error("Application.NotFound", "Application not found"));
        }

        string reviewerId = currentUser.UserAccountId?.ToString() ?? "system";

        application.Approve();

        var decision = FasApplicationReviewDecision.CreateApproval(application.Id, reviewerId, command.Remarks);
        await repository.AddDecisionAsync(decision, cancellationToken);
        
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ApproveApplicationResponse>.Success(
            new ApproveApplicationResponse(application.Id, application.StatusCode, decision.Decision)
        );
    }
}
