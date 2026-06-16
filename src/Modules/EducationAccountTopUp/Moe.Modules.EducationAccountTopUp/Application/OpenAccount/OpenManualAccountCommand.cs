using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.OpenAccount;

public sealed record OpenManualAccountCommand(
    long PersonId,
    string ReasonCode,
    string Remarks) : ICommand<OpenManualAccountResponse>;
