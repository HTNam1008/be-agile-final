using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.AdminAccountDetails;

public sealed record GetAdminAccountDetailsQuery(long PersonId) : IQuery<AdminAccountDetailsResponse>;
