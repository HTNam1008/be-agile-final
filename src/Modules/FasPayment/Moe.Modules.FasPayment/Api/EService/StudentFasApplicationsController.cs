using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.FasPayment.Api;
using Moe.Modules.FasPayment.Application.StudentApplications;

namespace Moe.Modules.FasPayment.Api.EService;

[ApiController, ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/fas")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal), EnableCors("EServiceCors")]
[ServiceFilter(typeof(FasApiExceptionFilter))]
public sealed class StudentFasApplicationsController(StudentFasApplicationService service) : ControllerBase
{
    [HttpGet("schemes")] public Task<object> Schemes(CancellationToken ct) => service.ListSchemes(ct);
    [HttpGet("schemes/{id:long}")] public Task<object> Scheme(long id, CancellationToken ct) => service.SchemeDetail(id, ct);
    [HttpPost("eligibility/check")] public Task<object> Eligibility(EligibilityRequest request, CancellationToken ct) => service.CheckEligibility(request, ct);
    [HttpGet("applications/prefill")] public Task<object> Prefill(CancellationToken ct) => service.Prefill(ct);
    [HttpGet("applications/me")] public Task<object> Mine(CancellationToken ct) => service.MyApplications(ct);
    [HttpGet("applications/me/summary")] public Task<object> Summary(CancellationToken ct) => service.Summary(ct);
    [HttpPost("applications/draft")] public Task<object> Draft(CreateDraftRequest request, CancellationToken ct) => service.CreateOrResumeDraft(request, ct);
    [HttpPut("applications/{id:long}/schemes")] public Task<object> Schemes(long id, ReplaceSchemesRequest request, CancellationToken ct) => service.ReplaceSchemes(id, request, ct);
    [HttpPut("applications/{id:long}/particulars")] public Task<object> Particulars(long id, UpdateParticularsRequest request, CancellationToken ct) => service.UpdateParticulars(id, request, ct);
    [HttpPut("applications/{id:long}/income")] public Task<object> Income(long id, UpdateIncomeRequest request, CancellationToken ct) => service.UpdateIncome(id, request, ct);
    [HttpGet("applications/{id:long}/required-documents")] public Task<object> RequiredDocuments(long id, CancellationToken ct) => service.RequiredDocuments(id, ct);
    [HttpPost("applications/{id:long}/documents"), RequestSizeLimit(10 * 1024 * 1024 + 65536)]
    public async Task<object> Upload(long id, [FromForm] string checklistItemCode, [FromForm] IFormFile file, CancellationToken ct)
    { await using var stream = file.OpenReadStream(); return await service.UploadDocument(id, checklistItemCode, file.FileName, file.ContentType, file.Length, stream, ct); }
    [HttpDelete("applications/{id:long}/documents/{documentId:long}")] public async Task<IActionResult> Remove(long id, long documentId, CancellationToken ct) { await service.RemoveDocument(id, documentId, ct); return NoContent(); }
    [HttpPost("applications/{id:long}/documents/{documentId:long}/replace"), RequestSizeLimit(10 * 1024 * 1024 + 65536)]
    public async Task<object> Replace(long id, long documentId, [FromForm] IFormFile file, CancellationToken ct) { await using var stream = file.OpenReadStream(); return await service.ReplaceDocument(id, documentId, file.FileName, file.ContentType, file.Length, stream, ct); }
    [HttpGet("documents/{documentId:long}/download")] public async Task<IActionResult> Download(long documentId, CancellationToken ct) { var d = await service.DownloadDocument(documentId, ct); return File(d.Stream, d.Mime, d.Name); }
    [HttpGet("applications/{id:long}/review-validation")] public Task<object> Review(long id, CancellationToken ct) => service.ReviewValidation(id, ct);
    [HttpGet("applications/{id:long}/review")] public Task<object> ApplicationReview(long id, CancellationToken ct) => service.ApplicationReview(id, ct);
    [HttpPut("applications/{id:long}/declarations")] public Task<object> Declarations(long id, SaveDeclarationsRequest request, CancellationToken ct) => service.SaveDeclarations(id, request, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), ct);
    [HttpPost("applications/{id:long}/submit")] public Task<object> Submit(long id, CancellationToken ct) => service.Submit(id, ct);
    [HttpPost("applications/{id:long}/withdraw")] public Task<object> Withdraw(long id, CancellationToken ct) => service.Withdraw(id, ct);
    [HttpPost("application-schemes/{id:long}/activate")] public Task<object> Activate(long id, CancellationToken ct) => service.Activate(id, ct);
}
