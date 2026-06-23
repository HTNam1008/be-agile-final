using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.CloseAccount;

public sealed record CloseManualAccountCommand(
    long EducationAccountId,
    string ReasonCode,
    string? Remarks) : ICommand<CloseManualAccountResponse>;

public sealed record CloseManualAccountResponse(
    long PersonId,
    long EducationAccountId,
    string StatusCode,
    DateTimeOffset? ClosedAtUtc);
