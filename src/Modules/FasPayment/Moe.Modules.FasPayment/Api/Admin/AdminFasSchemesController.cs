using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Infrastructure.Shared.Validation;
using Moe.Modules.FasPayment.Application.AdminFasSchemes;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/fas/schemes")]
[Authorize(Policy = AuthorizationPolicies.ManageFasSchemes)]
[EnableCors("AdminCors")]
[UnprocessableEntityOnModelValidation]
public sealed class AdminFasSchemesController(ICommandDispatcher commands, IQueryDispatcher queries) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListFasSchemesRequest request, CancellationToken cancellationToken)
        => Map(await queries.Send(new ListFasSchemesQuery(request.Status, request.Search), cancellationToken));

    [HttpGet("{schemeId:long}")]
    public async Task<IActionResult> Get(long schemeId, CancellationToken cancellationToken)
        => Map(await queries.Send(new GetFasSchemeQuery(schemeId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFasSchemeRequest request, CancellationToken cancellationToken)
    {
        Result<CreateFasSchemeResponse> result = await commands.Send(new CreateFasSchemeCommand(request), cancellationToken);
        return result.IsSuccess ? ApiResponseFactory.Created(result.Value, HttpContext.TraceIdentifier, "FAS scheme created.") : Failure(result.Error);
    }

    [HttpPost("draft")]
    public async Task<IActionResult> CreateDraft([FromBody] CreateFasSchemeRequest request, CancellationToken cancellationToken)
    {
        Result<CreateFasSchemeResponse> result = await commands.Send(new SaveFasSchemeDraftCommand(null, request), cancellationToken);
        return result.IsSuccess ? ApiResponseFactory.Created(result.Value, HttpContext.TraceIdentifier, "FAS draft saved.") : Failure(result.Error);
    }

    [HttpPut("{schemeId:long}")]
    [HttpPut("{schemeId:long}/draft")]
    public async Task<IActionResult> UpdateDraft(long schemeId, [FromBody] CreateFasSchemeRequest request, CancellationToken cancellationToken)
        => Map(await commands.Send(new SaveFasSchemeDraftCommand(schemeId, request), cancellationToken));

    [HttpPut("{schemeId:long}/activate")]
    public async Task<IActionResult> ActivateDraft(long schemeId, [FromBody] CreateFasSchemeRequest request, CancellationToken cancellationToken)
        => Map(await commands.Send(new ActivateFasSchemeDraftCommand(schemeId, request), cancellationToken));

    [HttpDelete("{schemeId:long}/draft")]
    public async Task<IActionResult> DeleteDraft(long schemeId, CancellationToken cancellationToken)
        => Map(await commands.Send(new DeleteFasSchemeDraftCommand(schemeId), cancellationToken));

    [HttpPost("{schemeId:long}/publish")]
    public async Task<IActionResult> Publish(long schemeId, CancellationToken cancellationToken)
        => Map(await commands.Send(new PublishFasSchemeCommand(schemeId), cancellationToken));

    [HttpPost("{schemeId:long}/disable")]
    public async Task<IActionResult> Disable(long schemeId, CancellationToken cancellationToken)
        => Map(await commands.Send(new DisableFasSchemeCommand(schemeId), cancellationToken));

    [HttpDelete("{schemeId:long}")]
    public async Task<IActionResult> Delete(long schemeId, CancellationToken cancellationToken)
        => Map(await commands.Send(new DeleteFasSchemeCommand(schemeId), cancellationToken));

    private IActionResult Map<T>(Result<T> result)
        => result.IsSuccess ? ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier) : Failure(result.Error);

    private IActionResult Failure(Error error)
    {
        int status = error.Code switch
        {
            "FAS.SCHEME_NOT_FOUND" => ApiResponseCodes.NotFound,
            "FAS.ACTOR_REQUIRED" => ApiResponseCodes.Unauthorized,
            _ => ApiResponseCodes.UnprocessableEntity
        };
        return ApiResponseFactory.Failure(error, status, HttpContext.TraceIdentifier);
    }
}
