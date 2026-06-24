using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.AdminAccountDetails;

public sealed record UpdateAdminAccountDetailsCommand(
    long PersonId,
    string? ClassCode,
    string? ResidentialAddress,
    string? Email,
    string? ContactNumber,
    DateTime? ExpectedUpdatedAtUtc) : ICommand<AdminAccountDetailsResponse>;
