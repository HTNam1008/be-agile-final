using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Application.History;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.TransactionResults;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/top-up/runs")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class TopUpTransactionResultsController(IQueryDispatcher queries)
    : ControllerBase
{
    /// <summary>
    /// Returns paged per-recipient results for a top-up run.
    /// </summary>
    [HttpGet("{runId:long}/transactions")]
    [Authorize(Policy = AuthorizationPolicies.ViewTopUps)]
    [ProducesResponseType(
        typeof(ApiResponse<PageResponse<TopUpTransactionResultItem>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        long runId,
        [FromQuery] TopUpTransactionResultsRequest request,
        CancellationToken cancellationToken)
    {
        TopUpTransactionResultFilter filter = new(
            request.Status,
            request.StudentOrAccountSearch,
            request.Reason,
            request.DateFromUtc,
            request.DateToUtc);

        Result<PageResponse<TopUpTransactionResultItem>> result =
            await queries.Send(
                new GetTopUpTransactionResultsQuery(
                    runId,
                    filter,
                    request.Page,
                    request.PageSize),
                cancellationToken);

        if (result.IsFailure)
        {
            return ApiResponseFactory.Failure(
                result.Error,
                GetStatusCode(result.Error),
                HttpContext.TraceIdentifier);
        }

        return ApiResponseFactory.Ok(
            result.Value,
            HttpContext.TraceIdentifier,
            "Top-up transaction results retrieved.");
    }

    private static int GetStatusCode(Error error)
        => error == TopUpErrors.RunNotFound
            ? ApiResponseCodes.NotFound
            : error == TopUpHistoryErrors.AccessDenied
                || error == TopUpHistoryErrors.OrganizationOutsideScope
                || error == TopUpHistoryErrors.OrganizationScopeRequired
                    ? ApiResponseCodes.Forbidden
                    : ApiResponseCodes.BadRequest;
}
