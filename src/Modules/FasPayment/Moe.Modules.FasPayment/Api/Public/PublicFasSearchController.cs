using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.FasPayment.Application.PublicSearch;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Api.Public;

[ApiController]
[ApiVersion(1.0)]
[Route("api/public/v{version:apiVersion}/fas")]
[AllowAnonymous]
[EnableCors("EServiceCors")]
public sealed class PublicFasSearchController(PublicFasSearchService service) : ControllerBase
{
    [HttpGet("schools")]
    public async Task<IActionResult> Schools(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await service.ListSchools(cancellationToken), HttpContext.TraceIdentifier);

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] PublicFasSearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            PublicFasSearchResult? result = await service.Search(request, cancellationToken);
            return result is null
                ? ApiResponseFactory.Failure(
                    new Error("FAS.SCHOOL_NOT_FOUND", "The selected school is not available."),
                    ApiResponseCodes.NotFound,
                    HttpContext.TraceIdentifier)
                : ApiResponseFactory.Ok(result, HttpContext.TraceIdentifier);
        }
        catch (ArgumentException)
        {
            return ApiResponseFactory.Failure(
                new Error("FAS.INVALID_PUBLIC_SEARCH", "Select a valid school and enter valid household details."),
                ApiResponseCodes.UnprocessableEntity,
                HttpContext.TraceIdentifier);
        }
    }
}
