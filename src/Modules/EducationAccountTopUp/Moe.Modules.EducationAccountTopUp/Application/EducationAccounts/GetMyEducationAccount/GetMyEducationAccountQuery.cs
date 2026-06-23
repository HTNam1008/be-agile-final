using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.EducationAccounts.GetMyEducationAccount;

public sealed record GetMyEducationAccountQuery(long PersonId) : IQuery<MyEducationAccountDto>;
