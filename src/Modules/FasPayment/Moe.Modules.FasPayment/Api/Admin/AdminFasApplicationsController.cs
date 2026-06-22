using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.FasPayment.Application.Applications.Approve;
using Moe.Modules.FasPayment.Application.Applications.GetApplicationDetail;
using Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications;
using Moe.Modules.FasPayment.Application.Applications.Reject;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/fas")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class AdminFasApplicationsController(ICommandDispatcher commands, IQueryDispatcher queries) : ControllerBase
{
    [HttpGet("schemes/{schemeId}/applications")]
    public async Task<IActionResult> GetSchemeApplications(long schemeId, CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetSchemeApplicationsQuery(schemeId), cancellationToken);
        return result.ToApiResponse(this, failureStatusCode: ApiResponseCodes.NotFound);
    }

    [HttpGet("applications/{applicationId}")]
    public async Task<IActionResult> GetApplicationDetail(long applicationId, CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetApplicationDetailQuery(applicationId), cancellationToken);
        return result.ToApiResponse(this, failureStatusCode: ApiResponseCodes.NotFound);
    }

    [HttpPost("applications/{applicationId}/approve")]
    public async Task<IActionResult> ApproveApplication(long applicationId, [FromBody] ApproveApplicationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await commands.Send(new ApproveApplicationCommand(applicationId, request.Remarks), cancellationToken);
            if (!result.IsSuccess)
            {
                var statusCode = result.Error.Code == "Application.NotFound" ? ApiResponseCodes.NotFound : ApiResponseCodes.Conflict;
                return ApiResponseFactory.Failure(result.Error, statusCode, HttpContext.TraceIdentifier);
            }
            return result.ToApiResponse(this);
        }
        catch (DomainException ex)
        {
            return ApiResponseFactory.Failure(new Error("Domain.Error", ex.Message), ApiResponseCodes.UnprocessableEntity, HttpContext.TraceIdentifier);
        }
    }

    [HttpPost("applications/{applicationId}/reject")]
    public async Task<IActionResult> RejectApplication(long applicationId, [FromBody] RejectApplicationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await commands.Send(new RejectApplicationCommand(applicationId, request.RejectionReasonCode, request.Remarks), cancellationToken);
            if (!result.IsSuccess)
            {
                var statusCode = result.Error.Code switch
                {
                    "Application.NotFound" => ApiResponseCodes.NotFound,
                    "Validation.Error" => ApiResponseCodes.UnprocessableEntity,
                    _ => ApiResponseCodes.Conflict
                };
                return ApiResponseFactory.Failure(result.Error, statusCode, HttpContext.TraceIdentifier);
            }
            return result.ToApiResponse(this);
        }
        catch (DomainException ex)
        {
            return ApiResponseFactory.Failure(new Error("Domain.Error", ex.Message), ApiResponseCodes.UnprocessableEntity, HttpContext.TraceIdentifier);
        }
    }
}

public sealed class ApproveApplicationRequest
{
    public string? Remarks { get; set; }
}

public sealed class RejectApplicationRequest
{
    public string RejectionReasonCode { get; set; } = string.Empty;
    public string? Remarks { get; set; }
}
