using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application.AdminUsers.CreateAdminUser;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/admin-users")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class AdminUsersController(ICommandDispatcher commands) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManageExternalAccounts)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        CreateAdminUserCommand command = new(
            request.Email,
            request.DisplayName,
            request.MailNickname,
            request.TemporaryPassword,
            request.InitialOrganizationUnitId,
            request.AccountEnabled);

        var result = await commands.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return ApiResponseFactory.Failure(
                result.Error,
                GetCreateAdminUserFailureStatusCode(result.Error.Code),
                HttpContext.TraceIdentifier);
        }

        return ApiResponseFactory.Created(
            result.Value,
            HttpContext.TraceIdentifier,
            "Admin user created.");
    }

    private static int GetCreateAdminUserFailureStatusCode(string errorCode)
        => errorCode switch
    {
        "IDENTITY.ADMIN_DIRECTORY_CREATE_FAILED" => ApiResponseCodes.BadGateway,
        "IDENTITY.ADMIN_LOCAL_ACCOUNT_CREATE_FAILED" => ApiResponseCodes.Conflict,
        _ => ApiResponseCodes.Conflict
    };
}
