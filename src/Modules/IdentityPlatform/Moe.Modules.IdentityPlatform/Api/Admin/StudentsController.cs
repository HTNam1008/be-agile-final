using Asp.Versioning;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Application.AdminAccountDetails;
using Moe.Modules.IdentityPlatform.Application.AdminStudentList;
using Moe.Modules.IdentityPlatform.Application.Students.BulkImportStudents;
using Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/students")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class StudentsController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    private const long BulkImportMaxFileSizeBytes = 5 * 1024 * 1024;
    private const string BulkImportTemplateContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ViewAccountDetails)]
    public async Task<IActionResult> List(
        [FromQuery] AdminStudentListRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queries.Send(
            new ListAdminStudentsQuery(
                request.OrganizationId,
                request.Search,
                string.IsNullOrWhiteSpace(request.LevelCode) ? [] : [request.LevelCode],
                request.ClassCode,
                request.AccountStatus,
                request.EnrollmentStatus,
                request.Page,
                request.PageSize,
                request.SortBy,
                request.SortDirection),
            cancellationToken);

        return result.IsFailure
            ? ApiResponseFactory.Failure(result.Error, GetFailureStatusCode(result.Error.Code), HttpContext.TraceIdentifier)
            : ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
    }

    [HttpGet("classes")]
    [Authorize(Policy = AuthorizationPolicies.ViewAccountDetails)]
    public async Task<IActionResult> ListClasses(
        [FromQuery] long organizationId,
        [FromQuery] string levelCode,
        CancellationToken cancellationToken = default)
    {
        var result = await queries.Send(
            new ListAdminStudentClassesQuery(organizationId, levelCode),
            cancellationToken);

        return result.IsFailure
            ? ApiResponseFactory.Failure(result.Error, GetFailureStatusCode(result.Error.Code), HttpContext.TraceIdentifier)
            : ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
    }

    [HttpGet("{personId:long}/account-details")]
    [Authorize(Policy = AuthorizationPolicies.ViewAccountDetails)]
    public async Task<IActionResult> GetAccountDetails(
        [FromRoute] long personId,
        CancellationToken cancellationToken)
    {
        var result = await queries.Send(new GetAdminAccountDetailsQuery(personId), cancellationToken);

        if (result.IsFailure)
        {
            return ApiResponseFactory.Failure(
                result.Error,
                GetFailureStatusCode(result.Error.Code),
                HttpContext.TraceIdentifier);
        }

        return ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
    }

    [HttpPut("{personId:long}/account-details")]
    [Authorize(Policy = AuthorizationPolicies.ManageAccountDetails)]
    public async Task<IActionResult> UpdateAccountDetails(
        [FromRoute] long personId,
        [FromBody] UpdateAdminAccountDetailsRequest request,
        CancellationToken cancellationToken)
    {
        UpdateAdminAccountDetailsCommand command = new(
            personId,
            request.ClassCode,
            request.ResidentialAddress,
            request.Email,
            request.ContactNumber,
            request.ExpectedUpdatedAtUtc);

        var result = await commands.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return ApiResponseFactory.Failure(
                result.Error,
                GetFailureStatusCode(result.Error.Code),
                HttpContext.TraceIdentifier);
        }

        return ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStudentRequest request,
        CancellationToken cancellationToken)
    {
        CreateStudentCommand command = new(
            request.SchoolName,
            request.OrganizationId,
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

    [HttpPost("bulk-import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> BulkImport(
        [FromForm] BulkImportStudentsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return ApiResponseFactory.Failure(
                new Moe.SharedKernel.Results.Error(
                    "BULK_IMPORT.FILE_REQUIRED",
                    "An Excel workbook file is required."),
                ApiResponseCodes.BadRequest,
                HttpContext.TraceIdentifier);
        }

        if (request.File.Length > BulkImportMaxFileSizeBytes)
        {
            return ApiResponseFactory.Failure(
                new Moe.SharedKernel.Results.Error(
                    "BULK_IMPORT.FILE_TOO_LARGE",
                    "The uploaded workbook must be 5 MB or smaller."),
                ApiResponseCodes.BadRequest,
                HttpContext.TraceIdentifier);
        }

        if (!request.File.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponseFactory.Failure(
                new Moe.SharedKernel.Results.Error(
                    "BULK_IMPORT.INVALID_FILE_TYPE",
                    "The uploaded file must be an .xlsx workbook."),
                ApiResponseCodes.BadRequest,
                HttpContext.TraceIdentifier);
        }

        await using Stream workbookStream = request.File.OpenReadStream();
        var result = await commands.Send(
            new BulkImportStudentsCommand(workbookStream, request.File.FileName),
            cancellationToken);

        return result.IsFailure
            ? ApiResponseFactory.Failure(result.Error, GetFailureStatusCode(result.Error.Code), HttpContext.TraceIdentifier)
            : ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
    }

    [HttpGet("bulk-import/template")]
    public IActionResult DownloadBulkImportTemplate()
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Students");

        for (int i = 0; i < BulkImportStudentWorkbookColumns.Headers.Count; i++)
        {
            string header = BulkImportStudentWorkbookColumns.Headers[i];
            sheet.Cell(1, i + 1).Value = header;
            sheet.Cell(2, i + 1).Value = BulkImportStudentWorkbookColumns.NullableHeaders.Contains(header)
                ? "Nullable - DELETE THIS ROW BEFORE IMPORT"
                : "Required - DELETE THIS ROW BEFORE IMPORT";
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(2).Style.Font.Italic = true;
        sheet.Row(2).Style.Fill.BackgroundColor = XLColor.LightYellow;
        sheet.Columns().AdjustToContents();

        using MemoryStream stream = new();
        workbook.SaveAs(stream);

        return File(
            stream.ToArray(),
            BulkImportTemplateContentType,
            "student-bulk-import-template.xlsx");
    }

    private static int GetFailureStatusCode(string errorCode)
        => errorCode switch
        {
            "BULK_IMPORT.FILE_REQUIRED" => ApiResponseCodes.BadRequest,
            "BULK_IMPORT.FILE_TOO_LARGE" => ApiResponseCodes.BadRequest,
            "BULK_IMPORT.INVALID_FILE_TYPE" => ApiResponseCodes.BadRequest,
            "BULK_IMPORT.ROW_LIMIT_EXCEEDED" => ApiResponseCodes.BadRequest,
            "BULK_IMPORT.ROW_VALIDATION_FAILED" => ApiResponseCodes.BadRequest,
            "IDENTITY.AUTHENTICATED_ADMIN_REQUIRED" => ApiResponseCodes.Unauthorized,
            "IDENTITY.SCHOOL_OUTSIDE_SCOPE" => ApiResponseCodes.Forbidden,
            "AUTH.ORGANIZATION_OUTSIDE_SCOPE" => ApiResponseCodes.Forbidden,
            "IDENTITY.ORGANIZATION_UNIT_NOT_FOUND" => ApiResponseCodes.NotFound,
            "IDENTITY.SCHOOL_REQUIRED" => ApiResponseCodes.Conflict,
            "IDENTITY.SCHOOL_IDENTIFIERS_CONFLICT" => ApiResponseCodes.Conflict,
            "IDENTITY.PERSON_NOT_FOUND" => ApiResponseCodes.NotFound,
            "IDENTITY.EDUCATION_ACCOUNT_NOT_FOUND" => ApiResponseCodes.NotFound,
            "IDENTITY.PROFILE_UPDATE_CONFLICT" => ApiResponseCodes.Conflict,
            "IDENTITY.ACTIVE_SCHOOL_ENROLLMENT_REQUIRED" => ApiResponseCodes.Conflict,
            "IDENTITY.STUDENT_ACCOUNT_CREATE_FAILED" => ApiResponseCodes.Conflict,
            _ => ApiResponseCodes.Conflict
        };
}
