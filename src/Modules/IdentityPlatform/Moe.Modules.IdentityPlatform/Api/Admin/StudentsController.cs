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
using Moe.Modules.IdentityPlatform.Application.Students.SetStudentAccess;
using Moe.Modules.IdentityPlatform.Domain.Schooling;

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
                request.CitizenshipStatusCode,
                request.AccountStatus,
                request.PortalAccessStatus,
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

    [HttpPost("{personId:long}/disable-access")]
    [Authorize(Policy = AuthorizationPolicies.ManageAccountDetails)]
    public async Task<IActionResult> DisableAccess(
        [FromRoute] long personId,
        CancellationToken cancellationToken)
    {
        var result = await commands.Send(new DisableStudentAccessCommand(personId), cancellationToken);

        return result.IsFailure
            ? ApiResponseFactory.Failure(result.Error, GetFailureStatusCode(result.Error.Code), HttpContext.TraceIdentifier)
            : ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
    }

    [HttpPost("{personId:long}/enable-access")]
    [Authorize(Policy = AuthorizationPolicies.ManageAccountDetails)]
    public async Task<IActionResult> EnableAccess(
        [FromRoute] long personId,
        CancellationToken cancellationToken)
    {
        var result = await commands.Send(new EnableStudentAccessCommand(personId), cancellationToken);

        return result.IsFailure
            ? ApiResponseFactory.Failure(result.Error, GetFailureStatusCode(result.Error.Code), HttpContext.TraceIdentifier)
            : ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier);
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
            request.ContactNumber,
            request.Address,
            IsAccountHolder: request.IsAccountHolder,
            MockPassPersonId: request.MockPassPersonId);

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
        AddBulkImportInstructionsSheet(workbook);

        for (int i = 0; i < BulkImportStudentWorkbookColumns.Headers.Count; i++)
        {
            string header = BulkImportStudentWorkbookColumns.Headers[i];
            IXLCell headerCell = sheet.Cell(1, i + 1);
            bool isOptional = BulkImportStudentWorkbookColumns.NullableHeaders.Contains(header);
            headerCell.Value = header;
            headerCell.Style.Font.Bold = true;
            headerCell.Style.Fill.BackgroundColor = isOptional ? XLColor.LightGray : XLColor.LightGreen;
            headerCell.CreateComment().AddText($"{(isOptional ? "Optional" : "Required")}: {GetBulkImportColumnHint(header)}");
        }

        int markerColumn = BulkImportStudentWorkbookColumns.Headers.Count + 1;
        sheet.Cell(1, markerColumn).Value = BulkImportStudentWorkbookColumns.TemplateRowMarker;
        sheet.Cell(1, markerColumn).Style.Font.Bold = true;
        sheet.Cell(1, markerColumn).Style.Font.FontColor = XLColor.Gray;
        sheet.Cell(1, markerColumn).Style.Font.Italic = true;
        sheet.Cell(1, markerColumn).Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.Cell(1, markerColumn).CreateComment()
            .AddText("Keep SAMPLE_DO_NOT_IMPORT only on the sample row. Clear this cell when replacing the sample row with real data.");

        WriteBulkImportSampleRow(sheet);
        AddBulkImportLevelValidation(sheet);

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(2).Style.Font.Italic = true;
        sheet.Row(2).Style.Fill.BackgroundColor = XLColor.LightYellow;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        using MemoryStream stream = new();
        workbook.SaveAs(stream);

        return File(
            stream.ToArray(),
            BulkImportTemplateContentType,
            "student-bulk-import-template.xlsx");
    }

    private static void AddBulkImportInstructionsSheet(XLWorkbook workbook)
    {
        IXLWorksheet instructions = workbook.Worksheets.Add("Instructions");
        instructions.Cell(1, 1).Value = "How to use this template";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        instructions.Cell(2, 1).Value = "1. Use the Students sheet for import data. Do not rename the header row.";
        instructions.Cell(3, 1).Value = "2. Row 2 is a sample and is ignored while TemplateRow is SAMPLE_DO_NOT_IMPORT.";
        instructions.Cell(4, 1).Value = "3. Replace row 2 with real data and clear TemplateRow, or start entering real data from row 3.";
        instructions.Cell(5, 1).Value = "4. Green headers are required. Gray headers are optional.";
        instructions.Cell(6, 1).Value = "5. LevelCode must be one of POST_SEC, BACHELOR, MASTER, PHD.";
        instructions.Columns().AdjustToContents();
    }

    private static void WriteBulkImportSampleRow(IXLWorksheet sheet)
    {
        sheet.Cell(2, 1).Value = "Sample Polytechnic";
        sheet.Cell(2, 2).Value = 10;
        sheet.Cell(2, 3).Value = "sample-mockpass-person-id";
        sheet.Cell(2, 4).Value = "S1234567D";
        sheet.Cell(2, 5).Value = "Sample Student";
        sheet.Cell(2, 6).Value = new DateTime(2008, 5, 12);
        sheet.Cell(2, 7).Value = "SG";
        sheet.Cell(2, 8).Value = "CITIZEN";
        sheet.Cell(2, 9).Value = "SAMPLE-2026-001";
        sheet.Cell(2, 10).Value = "2026";
        sheet.Cell(2, 11).Value = SchoolLevelCodes.Bachelor;
        sheet.Cell(2, 12).Value = "UG1";
        sheet.Cell(2, 13).Value = new DateTime(2026, 1, 2);
        sheet.Cell(2, 14).Value = "sample.student@example.com";
        sheet.Cell(2, 15).Value = "+6591234567";
        sheet.Cell(2, 16).Value = "1 Sample Road";
        IXLCell markerCell = sheet.Cell(2, BulkImportStudentWorkbookColumns.Headers.Count + 1);
        markerCell.Value = BulkImportStudentWorkbookColumns.SampleRowMarker;
        markerCell.Style.Font.FontColor = XLColor.Gray;
        markerCell.Style.Font.Italic = true;
        markerCell.Style.Fill.BackgroundColor = XLColor.LightGray;
        sheet.Cell(2, 6).Style.DateFormat.Format = "yyyy-mm-dd";
        sheet.Cell(2, 13).Style.DateFormat.Format = "yyyy-mm-dd";
    }

    private static void AddBulkImportLevelValidation(IXLWorksheet sheet)
    {
        int levelColumn = BulkImportStudentWorkbookColumns.Headers
            .Select((header, index) => new { header, index })
            .Single(x => x.header == BulkImportStudentWorkbookColumns.LevelCode)
            .index + 1;
        string validLevels = string.Join(",", SchoolLevelCodes.All);
        sheet.Range(2, levelColumn, 1001, levelColumn)
            .CreateDataValidation()
            .List($"\"{validLevels}\"");
    }

    private static string GetBulkImportColumnHint(string header)
        => header switch
        {
            BulkImportStudentWorkbookColumns.SchoolName => "School name. Optional when OrganizationId is supplied or school can be resolved.",
            BulkImportStudentWorkbookColumns.OrganizationId => "Numeric school organization id. Optional when SchoolName is supplied or school can be resolved.",
            BulkImportStudentWorkbookColumns.MockPassPersonId => "MockPass/Singpass subject id. Optional; use the UUID from mock-identities.json for demo login data.",
            BulkImportStudentWorkbookColumns.IdentityNumber => "Valid Singapore NRIC/FIN, including checksum. Example: S1234567D.",
            BulkImportStudentWorkbookColumns.FullName => "Student full name, maximum 200 characters.",
            BulkImportStudentWorkbookColumns.DateOfBirth => "Use a real Excel date or yyyy-mm-dd. Student age must be 6 to 40 years.",
            BulkImportStudentWorkbookColumns.NationalityCode => "Nationality code, maximum 30 characters. Example: SG.",
            BulkImportStudentWorkbookColumns.CitizenshipStatusCode => "Citizenship code, maximum 30 characters. Example: CITIZEN.",
            BulkImportStudentWorkbookColumns.StudentNumber => "School student number, maximum 50 characters.",
            BulkImportStudentWorkbookColumns.AcademicYear => "Academic year text, maximum 20 characters. Example: 2026.",
            BulkImportStudentWorkbookColumns.LevelCode => "Choose one: POST_SEC, BACHELOR, MASTER, PHD.",
            BulkImportStudentWorkbookColumns.ClassCode => "Class code, maximum 30 characters.",
            BulkImportStudentWorkbookColumns.StartDate => "Use a real Excel date or yyyy-mm-dd. Must not be earlier than DateOfBirth.",
            BulkImportStudentWorkbookColumns.Email => "Valid email address, maximum 320 characters.",
            BulkImportStudentWorkbookColumns.ContactNumber => "Mobile/contact number, maximum 50 characters.",
            BulkImportStudentWorkbookColumns.Address => "Residential address, maximum 1000 characters.",
            _ => "Follow the column format shown in the sample row."
        };

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
