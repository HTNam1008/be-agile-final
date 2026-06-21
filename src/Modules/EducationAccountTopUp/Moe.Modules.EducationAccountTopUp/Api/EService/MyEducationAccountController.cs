using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.EducationAccounts.GetMyEducationAccount;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Api.EService;

[ApiController]
[ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/my-education-account")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal)]
[EnableCors("EServiceCors")]
public sealed class MyEducationAccountController(
    IQueryDispatcher dispatcher,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<MyEducationAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.PersonId is null)
        {
            return ApiResponseFactory.Failure(
                EducationAccountErrors.AuthenticatedStudentRequired,
                ApiResponseCodes.Unauthorized,
                HttpContext.TraceIdentifier);
        }

        Result<MyEducationAccountDto> result = await dispatcher.Send(new GetMyEducationAccountQuery(currentUser.PersonId.Value), cancellationToken);

        if (result.IsFailure)
        {
            return ApiResponseFactory.Failure(
                result.Error,
                result.Error.Code == EducationAccountErrors.NotFound.Code ? ApiResponseCodes.NotFound : ApiResponseCodes.BadRequest,
                HttpContext.TraceIdentifier);
        }

        return ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
    }
}
