using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;
using Moe.Modules.IdentityPlatform.IGateway.Students;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/students")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class StudentsController(
    ICommandDispatcher commands,
    IStudentDirectory students,
    IAdminAccessControl adminAccess) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] long organizationId,
        [FromQuery] string? search,
        [FromQuery] string? levelCode,
        [FromQuery] string? classCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (organizationId <= 0)
        {
            return ApiResponseFactory.Failure(
                IdentityErrors.OrganizationUnitNotFound,
                ApiResponseCodes.NotFound,
                HttpContext.TraceIdentifier);
        }

        if (!adminAccess.CanAccessOrganization(organizationId))
        {
            return ApiResponseFactory.Failure(
                IdentityErrors.SchoolOutsideScope,
                ApiResponseCodes.Forbidden,
                HttpContext.TraceIdentifier);
        }

        AdminStudentSearchCriteria criteria = new(
            organizationId,
            search,
            levelCode,
            classCode,
            Math.Max(page, 1),
            Math.Clamp(pageSize, 1, 100));

        long totalCount = await students.CountByOrganizationAsync(criteria, cancellationToken);
        IReadOnlyList<AdminStudentSearchSummary> items = await students.ListByOrganizationAsync(
            criteria,
            cancellationToken);

        return ApiResponseFactory.Ok(
            new PageResponse<AdminStudentSearchSummary>(
                items,
                criteria.Page,
                criteria.PageSize,
                totalCount),
            HttpContext.TraceIdentifier);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStudentRequest request,
        CancellationToken cancellationToken)
    {
        CreateStudentCommand command = new(
            request.SchoolName,
            request.IdentityNumber,
            request.FullName,
            request.DateOfBirth,
            request.NationalityCode,
            request.CitizenshipStatusCode,
            request.StudentNumber,
            request.AcademicYear,
            request.LevelCode,
            request.ClassCode,
            request.StartDate,
            request.Email,
            request.Mobile,
            request.Address,
            request.IsAccountHolder);

        var result = await commands.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return ApiResponseFactory.Failure(
                result.Error,
                GetFailureStatusCode(result.Error.Code),
                HttpContext.TraceIdentifier);
        }

        return ApiResponseFactory.Created(
            result.Value,
            HttpContext.TraceIdentifier,
            "Student created.");
    }

    private static int GetFailureStatusCode(string errorCode)
        => errorCode switch
        {
            "IDENTITY.AUTHENTICATED_ADMIN_REQUIRED" => ApiResponseCodes.Unauthorized,
            "IDENTITY.SCHOOL_OUTSIDE_SCOPE" => ApiResponseCodes.Forbidden,
            "IDENTITY.ORGANIZATION_UNIT_NOT_FOUND" => ApiResponseCodes.NotFound,
            "IDENTITY.STUDENT_ACCOUNT_CREATE_FAILED" => ApiResponseCodes.Conflict,
            _ => ApiResponseCodes.Conflict
        };
}
