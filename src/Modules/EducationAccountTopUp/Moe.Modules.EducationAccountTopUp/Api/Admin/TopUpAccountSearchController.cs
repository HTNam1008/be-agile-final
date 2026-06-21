using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.SearchAccounts;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/top-up/accounts")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class TopUpAccountSearchController(IQueryDispatcher queries) : ControllerBase
{
    [HttpGet("search")]
    [Authorize(Policy = AuthorizationPolicies.ManageTopUps)]
    public async Task<IActionResult> Search(
        [FromQuery] SearchTopUpAccountsRequest request,
        CancellationToken cancellationToken)
    {
        SearchTopUpAccountsQuery query = new(
            request.Search,
            request.OrganizationId,
            request.SchoolingStatusCode,
            request.LevelCode,
            request.ClassCode,
            request.AccountStatusCode,
            request.AgeFrom,
            request.AgeTo,
            request.BalanceFrom,
            request.BalanceTo,
            request.Page,
            request.PageSize);

        var result = await queries.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return TopUpErrorResponseMapper.ToFailureResponse(result.Error, HttpContext);
        }

        return ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
    }


}
