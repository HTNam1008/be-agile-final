using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.Students.SetStudentAccess;

internal sealed class EnableStudentAccessHandler(
    IPersonLifecycleGateway people,
    IClock clock,
    IUnitOfWork unitOfWork) : ICommandHandler<EnableStudentAccessCommand, StudentAccessResponse>
{
    public async Task<Result<StudentAccessResponse>> Handle(EnableStudentAccessCommand command, CancellationToken cancellationToken)
    {
        Result result = await people.EnableAsync(command.PersonId, clock.UtcNow.UtcDateTime, cancellationToken);
        if (result.IsFailure)
        {
            return Result<StudentAccessResponse>.Failure(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<StudentAccessResponse>.Success(new StudentAccessResponse(command.PersonId, PersonStatusCodes.Active));
    }
}
