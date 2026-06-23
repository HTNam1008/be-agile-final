using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Api;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Api;

internal static class TopUpErrorResponseMapper
{
    public static ObjectResult ToFailureResponse(Error error, HttpContext httpContext)
    {
        int statusCode = error.Code switch
        {
            "TopUp.CampaignNotFound" => ApiResponseCodes.NotFound,
            "TopUp.RunNotFound" => ApiResponseCodes.NotFound,
            "TopUp.AccountNotFound" => ApiResponseCodes.NotFound,
            "ACCOUNT.NOT_FOUND" => ApiResponseCodes.NotFound,

            "TopUp.Unauthorized" => ApiResponseCodes.Forbidden,
            "TopUp.OrganizationOutsideScope" => ApiResponseCodes.Forbidden,
            "TopUp.AccountSelectionOutsideScope" => ApiResponseCodes.Forbidden,
            "TopUp.AdminOrganizationScopeRequired" => ApiResponseCodes.Forbidden,

            "TOPUP.HISTORY_ACCESS_DENIED" => ApiResponseCodes.Forbidden,
            "TOPUP.HISTORY_ORGANIZATION_OUTSIDE_SCOPE" => ApiResponseCodes.Forbidden,
            "TOPUP.HISTORY_ORGANIZATION_SCOPE_REQUIRED" => ApiResponseCodes.Forbidden,

            "TopUp.ActorRequired" => ApiResponseCodes.Unauthorized,
            "ACCOUNT.AUTHENTICATED_STUDENT_REQUIRED" => ApiResponseCodes.Unauthorized,

            _ => ApiResponseCodes.BadRequest
        };

        return ApiResponseFactory.Failure(error, statusCode, httpContext.TraceIdentifier);
    }
}
