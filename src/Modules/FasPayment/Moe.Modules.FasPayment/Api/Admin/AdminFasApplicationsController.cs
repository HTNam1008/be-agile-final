using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.FasPayment.Api;
using Moe.Modules.FasPayment.Application.Applications.Approve;
using Moe.Modules.FasPayment.Application.Applications.GetApplicationDetail;
using Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications;
using Moe.Modules.FasPayment.Application.Applications.Reject;
using Moe.Modules.FasPayment.Application.StudentApplications;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/fas")]
[Authorize(Policy = AuthorizationPolicies.ReviewFas)]
[EnableCors("AdminCors")]
[ServiceFilter(typeof(FasApiExceptionFilter))]
public sealed class AdminFasApplicationsController(ICommandDispatcher commands, IQueryDispatcher queries, StudentFasApplicationService? studentApplications = null) : ControllerBase
{
    [HttpGet("applications")]
    public Task<object> GetApplications(
        [FromQuery] string? status,
        [FromQuery] long? schemeId,
        [FromQuery] string? keyword,
        [FromQuery] DateOnly? submittedFrom,
        [FromQuery] DateOnly? submittedTo,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDirection,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
        => (studentApplications ?? throw new InvalidOperationException("FAS service is unavailable.")).AdminApplications(status, schemeId, keyword, submittedFrom, submittedTo, sortBy, sortDirection, page, pageSize, cancellationToken);

    [HttpGet("applications/{applicationId:long}/full")]
    public Task<object> GetFullApplication(long applicationId, CancellationToken cancellationToken)
        => (studentApplications ?? throw new InvalidOperationException("FAS service is unavailable.")).AdminApplication(applicationId, cancellationToken);

    [HttpPost("application-schemes/{id:long}/approve")]
    public Task<object> ApproveScheme(long id, AdminApproveSchemeRequest request, CancellationToken cancellationToken)
        => (studentApplications ?? throw new InvalidOperationException("FAS service is unavailable.")).ApproveScheme(id, request, cancellationToken);

    [HttpPost("application-schemes/{id:long}/reject")]
    public Task<object> RejectScheme(long id, AdminRejectSchemeRequest request, CancellationToken cancellationToken)
        => (studentApplications ?? throw new InvalidOperationException("FAS service is unavailable.")).RejectScheme(id, request, cancellationToken);

    [HttpGet("documents/{documentId:long}/download")]
    public async Task<IActionResult> DownloadDocument(long documentId, CancellationToken cancellationToken) { var d = await (studentApplications ?? throw new InvalidOperationException("FAS service is unavailable.")).AdminDownloadDocument(documentId, cancellationToken); return File(d.Stream, d.Mime, d.Name); }

    [HttpPost("documents/{documentId:long}/scan-result")]
    public Task<object> RecordScanResult(long documentId, DocumentScanResultRequest request, CancellationToken cancellationToken) => (studentApplications ?? throw new InvalidOperationException("FAS service is unavailable.")).RecordScanResult(documentId, request.Passed, cancellationToken);
    [HttpGet("schemes/{schemeId}/applications")]
    public async Task<IActionResult> GetSchemeApplications(long schemeId, CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetSchemeApplicationsQuery(schemeId), cancellationToken);
        return result.ToApiResponse(this, failureStatusCode: ApiResponseCodes.NotFound);
    }

    [HttpGet("applications/{applicationId}")]
    public Task<object> GetApplicationDetail(long applicationId, CancellationToken cancellationToken)
        => (studentApplications ?? throw new InvalidOperationException("FAS service is unavailable.")).AdminApplication(applicationId, cancellationToken);

    [HttpGet("applications/{applicationId:long}/compat")]
    public async Task<IActionResult> GetCompatibleApplicationDetail(long applicationId, CancellationToken cancellationToken)
    { var result = await queries.Send(new GetApplicationDetailQuery(applicationId), cancellationToken); return result.ToApiResponse(this, failureStatusCode: ApiResponseCodes.NotFound); }

    [HttpPost("applications/{applicationId}/approve")]
    public async Task<IActionResult> ApproveApplication(long applicationId, [FromBody] ApproveApplicationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await commands.Send(new ApproveApplicationCommand(applicationId, request.Remarks), cancellationToken);
            if (!result.IsSuccess)
            {
                var statusCode = result.Error.Code == "Application.NotFound" ? ApiResponseCodes.NotFound : ApiResponseCodes.Conflict;
                return ApiResponseFactory.Failure(result.Error, statusCode, HttpContext?.TraceIdentifier ?? string.Empty);
            }
            return result.ToApiResponse(this);
        }
        catch (DomainException ex)
        {
            var failure = (ObjectResult)ApiResponseFactory.Failure(new Error("Domain.Error", ex.Message), ApiResponseCodes.UnprocessableEntity, HttpContext?.TraceIdentifier ?? string.Empty);
            return new UnprocessableEntityObjectResult(failure.Value);
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
                var failure = (ObjectResult)ApiResponseFactory.Failure(result.Error, statusCode, HttpContext?.TraceIdentifier ?? string.Empty);
                return statusCode == ApiResponseCodes.UnprocessableEntity ? new UnprocessableEntityObjectResult(failure.Value) : failure;
            }
            return result.ToApiResponse(this);
        }
        catch (DomainException ex)
        {
            var failure = (ObjectResult)ApiResponseFactory.Failure(new Error("Domain.Error", ex.Message), ApiResponseCodes.UnprocessableEntity, HttpContext?.TraceIdentifier ?? string.Empty);
            return new UnprocessableEntityObjectResult(failure.Value);
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
