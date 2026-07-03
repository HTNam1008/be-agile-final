using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.FasPayment.Application.PublicSearch;

namespace Moe.Modules.FasPayment.Api.Public;

[ApiController]
[ApiVersion(1.0)]
[Route("api/public/v{version:apiVersion}/fas")]
[AllowAnonymous]
[EnableCors("PortalCors")]
[EnableRateLimiting("PublicFasSearch")]
public sealed class PublicFasController(PublicFasSearchService service) : ControllerBase
{
    [HttpGet("schools")]
    public async Task<IActionResult> Schools(CancellationToken cancellationToken)
    {
        var schools = await service.ListSchoolsAsync(cancellationToken);
        return ApiResponseFactory.Ok(schools, HttpContext.TraceIdentifier);
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search(
        [FromBody] PublicFasSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OrganizationId <= 0 ||
            request.MonthlyHouseholdIncome < 0 ||
            request.HouseholdMemberCount <= 0)
        {
            return new ObjectResult(ApiResponse<object>.Fail(
                "Select a school and enter valid household details.",
                ["FAS.INVALID_PUBLIC_SEARCH"],
                ApiResponseCodes.UnprocessableEntity,
                HttpContext.TraceIdentifier))
            {
                StatusCode = ApiResponseCodes.UnprocessableEntity
            };
        }

        var result = await service.SearchAsync(request, cancellationToken);
        if (result is null)
        {
            return new ObjectResult(ApiResponse<object>.Fail(
                "The selected school was not found.",
                ["FAS.SCHOOL_NOT_FOUND"],
                ApiResponseCodes.NotFound,
                HttpContext.TraceIdentifier))
            {
                StatusCode = ApiResponseCodes.NotFound
            };
        }

        return ApiResponseFactory.Ok(result, HttpContext.TraceIdentifier);
    }
}
