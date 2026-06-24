using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

namespace Moe.Modules.EducationAccountTopUp.IGateway.Accounts;

public sealed record AutomaticEducationAccountClosureResult(
    long EducationAccountId,
    long PersonId,
    bool Closed);

public sealed record AutomaticEducationAccountClosureSummary(
    int ActiveAccountCount,
    int ClosedCount,
    IReadOnlyCollection<AutomaticEducationAccountClosureResult> Results);

public interface IAutomaticEducationAccountCloser
{
    Task<AutomaticEducationAccountClosureSummary> CloseEligibleAsync(
        DateOnly today,
        DateTimeOffset closedAtUtc,
        CancellationToken cancellationToken);

    Task<AutomaticEducationAccountClosureResult> EnsureClosedAsync(
        EducationAccount account,
        DateTimeOffset closedAtUtc,
        CancellationToken cancellationToken);
}
