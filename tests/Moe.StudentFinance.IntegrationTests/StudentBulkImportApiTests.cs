using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Audit;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.Application.Students.BulkImportStudents;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class StudentBulkImportApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Endpoint = "/api/admin/v1/students/bulk-import";
    private const string TemplateEndpoint = "/api/admin/v1/students/bulk-import/template";
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task BulkImportTemplate_ReturnsValidWorkbookWithCurrentHeaders()
    {
        using HttpResponseMessage response = await _client.GetAsync(TemplateEndpoint);

        await AssertStatusAsync(HttpStatusCode.OK, response);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains(
            "student-bulk-import-template",
            response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName
                ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using XLWorkbook workbook = new(stream);
        IXLWorksheet sheet = workbook.Worksheet("Students");

        string[] headers = Enumerable.Range(1, BulkImportStudentWorkbookColumns.Headers.Count)
            .Select(index => sheet.Cell(1, index).GetString())
            .ToArray();

        Assert.Equal(BulkImportStudentWorkbookColumns.Headers, headers);
        Assert.True(workbook.Worksheets.Contains("Instructions"));
        Assert.True(sheet.LastRowUsed()?.RowNumber() >= 2);

        for (int i = 0; i < BulkImportStudentWorkbookColumns.Headers.Count; i++)
        {
            string header = BulkImportStudentWorkbookColumns.Headers[i];
            string note = sheet.Cell(1, i + 1).GetComment().Text;
            string expectedPrefix = BulkImportStudentWorkbookColumns.NullableHeaders.Contains(header)
                ? "Optional"
                : "Required";

            Assert.StartsWith(expectedPrefix, note);
        }

        Assert.Equal("TemplateRow", sheet.Cell(1, BulkImportStudentWorkbookColumns.Headers.Count + 1).GetString());
        Assert.Equal("SAMPLE_DO_NOT_IMPORT", sheet.Cell(2, BulkImportStudentWorkbookColumns.Headers.Count + 1).GetString());
        Assert.Equal("S1234567D", sheet.Cell(2, 3).GetString());
        Assert.Equal("BACHELOR", sheet.Cell(2, 10).GetString());
        Assert.NotNull(sheet.Cell(10, 10).GetDataValidation());
    }

    [Fact]
    public async Task BulkImportTemplate_WhenAnonymous_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, TemplateEndpoint);
        request.Headers.Add("X-Test-Unauthenticated", "true");

        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BulkImport_WithValidWorkbook_CreatesStudentsWithoutEducationAccountsOrImportAudit()
    {
        string suffix = UniqueSuffix();
        StudentImportRow[] rows =
        [
            ValidRow(suffix, "001"),
            ValidRow(suffix, "002") with { CitizenshipStatusCode = "" }
        ];

        using HttpResponseMessage response = await PostWorkbookAsync(rows);

        await AssertStatusAsync(HttpStatusCode.OK, response);
        BulkImportResponse result = await ReadBulkImportResponseAsync(response);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.All(result.Results, row => Assert.Equal("Succeeded", row.Status));

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        long[] personIds = result.Results.Select(x => x.PersonId!.Value).ToArray();

        Assert.Equal(2, await db.Set<Person>().CountAsync(x => personIds.Contains(x.Id)));
        Assert.Equal(2, await db.Set<SchoolEnrollment>().CountAsync(x => personIds.Contains(x.PersonId)));
        Assert.False(await db.Set<EducationAccount>().AnyAsync(x => personIds.Contains(x.PersonId)));
        Assert.False(AnyAuditLogDetailsContaining(db, suffix));

        long blankCitizenshipPersonId = result.Results.Single(x => x.RowNumber == 3).PersonId!.Value;
        Person blankCitizenshipPerson = await db.Set<Person>().SingleAsync(x => x.Id == blankCitizenshipPersonId);
        Assert.Null(blankCitizenshipPerson.CitizenshipStatusCode);

        long populatedCitizenshipPersonId = result.Results.Single(x => x.RowNumber == 2).PersonId!.Value;
        Person populatedCitizenshipPerson = await db.Set<Person>().SingleAsync(x => x.Id == populatedCitizenshipPersonId);
        Assert.Equal("CITIZEN", populatedCitizenshipPerson.CitizenshipStatusCode);
    }

    [Fact]
    public async Task BulkImport_WithHigherEducationLevel_CreatesStudent()
    {
        string suffix = UniqueSuffix();
        StudentImportRow row = ValidRow(suffix, "BACHELOR") with
        {
            LevelCode = "BACHELOR",
            ClassCode = ""
        };

        using HttpResponseMessage response = await PostWorkbookAsync([row]);

        await AssertStatusAsync(HttpStatusCode.OK, response);
        BulkImportResponse result = await ReadBulkImportResponseAsync(response);
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.SucceededCount);

        long personId = result.Results.Single().PersonId!.Value;
        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        SchoolEnrollment enrollment = await db.Set<SchoolEnrollment>().SingleAsync(x => x.PersonId == personId);
        Assert.Equal("BACHELOR", enrollment.LevelCode);
        Assert.Null(enrollment.ClassCode);
    }

    [Fact]
    public async Task BulkImport_WithDownloadedTemplateUnedited_SkipsSampleRow()
    {
        using HttpResponseMessage templateResponse = await _client.GetAsync(TemplateEndpoint);
        await AssertStatusAsync(HttpStatusCode.OK, templateResponse);
        byte[] workbook = await templateResponse.Content.ReadAsByteArrayAsync();

        using HttpResponseMessage response = await PostWorkbookBytesAsync(workbook);

        await AssertStatusAsync(HttpStatusCode.OK, response);
        BulkImportResponse result = await ReadBulkImportResponseAsync(response);
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(1, result.SkippedCount);
        BulkImportRowResult skipped = Assert.Single(result.Results);
        Assert.Equal(2, skipped.RowNumber);
        Assert.Equal("Skipped", skipped.Status);
        Assert.Equal("BULK_IMPORT.SAMPLE_ROW_SKIPPED", skipped.ErrorCode);
        Assert.Contains("SAMPLE_DO_NOT_IMPORT", skipped.ErrorMessage);
    }

    [Fact]
    public async Task BulkImport_WhenRealDataStillHasSampleMarker_ReturnsSkippedRow()
    {
        string suffix = UniqueSuffix();
        StudentImportRow row = ValidRow(suffix, "MARKED");
        byte[] workbook = CreateWorkbookWithSampleMarker(row);

        using HttpResponseMessage response = await PostWorkbookBytesAsync(workbook);

        await AssertStatusAsync(HttpStatusCode.OK, response);
        BulkImportResponse result = await ReadBulkImportResponseAsync(response);
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(1, result.SkippedCount);
        BulkImportRowResult skipped = Assert.Single(result.Results);
        Assert.Equal(2, skipped.RowNumber);
        Assert.Equal("Skipped", skipped.Status);
        Assert.Equal("BULK_IMPORT.SAMPLE_ROW_SKIPPED", skipped.ErrorCode);
    }

    [Fact]
    public async Task BulkImport_WhenTemplateSampleRowIsReplacedWithRealData_ImportsRow()
    {
        string suffix = UniqueSuffix();
        StudentImportRow row = ValidRow(suffix, "REPLACED");
        using HttpResponseMessage templateResponse = await _client.GetAsync(TemplateEndpoint);
        await AssertStatusAsync(HttpStatusCode.OK, templateResponse);
        await using Stream templateStream = await templateResponse.Content.ReadAsStreamAsync();
        using XLWorkbook workbook = new(templateStream);
        IXLWorksheet sheet = workbook.Worksheet("Students");
        WriteRow(sheet, rowNumber: 2, row);
        sheet.Cell(2, BulkImportStudentWorkbookColumns.Headers.Count + 1).Clear();
        byte[] content = SaveWorkbook(workbook);

        using HttpResponseMessage response = await PostWorkbookBytesAsync(content);

        await AssertStatusAsync(HttpStatusCode.OK, response);
        BulkImportResponse result = await ReadBulkImportResponseAsync(response);
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(2, result.Results.Single().RowNumber);
    }

    [Fact]
    public async Task BulkImport_AutoLifecycleTestDataWorkbook_ReturnsExpectedSeventeenSuccessAndThreeFailures()
    {
        byte[] workbook = await File.ReadAllBytesAsync(Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "test-data",
            "test-data-002-auto-lifecycle-bulk-import.xlsx"));

        using HttpResponseMessage response = await PostWorkbookBytesAsync(
            workbook,
            configureRequest: request => request.Headers.Add("X-Test-OrganizationUnitIds", "1,2"));

        await AssertStatusAsync(HttpStatusCode.OK, response);
        BulkImportResponse result = await ReadBulkImportResponseAsync(response);
        Assert.Equal(20, result.TotalRows);
        Assert.True(
            result.SucceededCount == 17,
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(3, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(3, result.Results.Count(x => x.Status == "Failed"));
        Assert.Contains(result.Results, x => x.RowNumber == 19
            && x.Status == "Failed"
            && x.ErrorMessage?.Contains("valid Singapore NRIC/FIN", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(result.Results, x => x.RowNumber == 20
            && x.Status == "Failed"
            && x.ErrorMessage?.Contains("'Full Name' must not be empty", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(result.Results, x => x.RowNumber == 21
            && x.Status == "Failed"
            && x.ErrorMessage?.Contains("Start date cannot be earlier than date of birth", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Theory]
    [InlineData("test-data-003-mockpass-import-demo.xlsx")]
    [InlineData("test-data-004-mockpass-import-demo.xlsx")]
    [InlineData("test-data-005-mockpass-import-demo.xlsx")]
    [InlineData("test-data-006-mockpass-import-demo.xlsx")]
    public async Task BulkImport_MockpassDemoWorkbook_ReturnsExpectedTwentySuccesses(string fileName)
    {
        byte[] workbook = await File.ReadAllBytesAsync(Path.Combine(FindRepositoryRoot(), "scripts", "test-data", fileName));

        using HttpResponseMessage response = await PostWorkbookBytesAsync(
            workbook,
            configureRequest: request => request.Headers.Add("X-Test-OrganizationUnitIds", "1,2,3,4,5,6"));

        await AssertStatusAsync(HttpStatusCode.OK, response);
        BulkImportResponse result = await ReadBulkImportResponseAsync(response);
        Assert.Equal(20, result.TotalRows);
        Assert.True(
            result.SucceededCount == 20,
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.All(result.Results, x => Assert.Equal("Succeeded", x.Status));
    }

    [Fact]
    public async Task BulkImport_WithMixedRows_ReturnsRowLevelFailuresAndKeepsSuccessfulRows()
    {
        string suffix = UniqueSuffix();
        StudentImportRow existing = ValidRow(suffix, "EXISTING");
        long existingPersonId = await SeedExistingStudentAsync(existing);
        StudentImportRow valid = ValidRow(suffix, "VALID");
        StudentImportRow duplicateInFileFirst = ValidRow(suffix, "DUP-A");
        StudentImportRow duplicateInFileSecond = duplicateInFileFirst with { StudentNumber = $"UM015-DUP-B-{suffix}" };
        StudentImportRow missingName = ValidRow(suffix, "MISSING") with { FullName = "" };
        StudentImportRow schoolConflict = ValidRow(suffix, "CONFLICT") with
        {
            OrganizationId = 1,
            SchoolName = "Other Secondary School"
        };

        using HttpResponseMessage response = await PostWorkbookAsync(
            [
                existing,
                valid,
                duplicateInFileFirst,
                duplicateInFileSecond,
                missingName,
                schoolConflict
            ]);

        await AssertStatusAsync(HttpStatusCode.OK, response);
        BulkImportResponse result = await ReadBulkImportResponseAsync(response);

        Assert.Equal(6, result.TotalRows);
        Assert.Equal(2, result.SucceededCount);
        Assert.Equal(4, result.FailedCount);
        Assert.Contains(result.Results, x => x.RowNumber == 2
            && x.Status == "Failed"
            && x.ErrorCode == "IDENTITY.STUDENT_IDENTITY_ALREADY_EXISTS");
        Assert.Contains(result.Results, x => x.RowNumber == 5
            && x.Status == "Failed"
            && x.ErrorCode == "IDENTITY.STUDENT_IDENTITY_ALREADY_EXISTS");
        Assert.Contains(result.Results, x => x.RowNumber == 6
            && x.Status == "Failed"
            && x.ErrorCode == "BULK_IMPORT.ROW_VALIDATION_FAILED");
        Assert.Contains(result.Results, x => x.RowNumber == 7
            && x.Status == "Failed"
            && x.ErrorCode == "IDENTITY.SCHOOL_IDENTIFIERS_CONFLICT");

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        Assert.True(await db.Set<Person>().AnyAsync(x => x.Id == existingPersonId));
        Assert.True(await db.Set<Person>().AnyAsync(x => x.IdentityNumberMasked == valid.IdentityNumber));
        Assert.True(await db.Set<Person>().AnyAsync(x => x.IdentityNumberMasked == duplicateInFileFirst.IdentityNumber));
        Assert.False(await db.Set<Person>().AnyAsync(x => x.IdentityNumberMasked == missingName.IdentityNumber));
        Assert.False(await db.Set<Person>().AnyAsync(x => x.IdentityNumberMasked == schoolConflict.IdentityNumber));
    }

    [Fact]
    public async Task BulkImport_WhenRowLimitExceeded_RejectsBeforeProcessingAnyRows()
    {
        string suffix = UniqueSuffix();
        StudentImportRow[] rows = Enumerable.Range(1, 1001)
            .Select(index => ValidRow(suffix, index.ToString("D4")))
            .ToArray();

        using HttpResponseMessage response = await PostWorkbookAsync(rows);

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("BULK_IMPORT.ROW_LIMIT_EXCEEDED", body);

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        Assert.False(await db.Set<Person>().AnyAsync(x => x.OfficialFullName.Contains(suffix)));
    }

    [Fact]
    public async Task BulkImport_WhenFileSizeLimitExceeded_RejectsBeforeParsingWorkbook()
    {
        string suffix = UniqueSuffix();
        byte[] oversizedContent = new byte[(5 * 1024 * 1024) + 1];

        using MultipartFormDataContent content = new();
        ByteArrayContent fileContent = new(oversizedContent);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", $"UM015-{suffix}.xlsx");

        using HttpResponseMessage response = await _client.PostAsync(Endpoint, content);

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("BULK_IMPORT.FILE_TOO_LARGE", body);
    }

    private async Task<long> SeedExistingStudentAsync(StudentImportRow row)
    {
        using HttpResponseMessage response = await PostWorkbookAsync([row]);
        await AssertStatusAsync(HttpStatusCode.OK, response);
        BulkImportResponse result = await ReadBulkImportResponseAsync(response);
        return result.Results.Single().PersonId!.Value;
    }

    private async Task<HttpResponseMessage> PostWorkbookAsync(IReadOnlyCollection<StudentImportRow> rows)
    {
        byte[] workbook = CreateWorkbook(rows);
        return await PostWorkbookBytesAsync(workbook);
    }

    private async Task<HttpResponseMessage> PostWorkbookBytesAsync(
        byte[] workbook,
        Action<HttpRequestMessage>? configureRequest = null)
    {
        using MultipartFormDataContent content = new();
        ByteArrayContent fileContent = new(workbook);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "students.xlsx");
        using HttpRequestMessage request = new(HttpMethod.Post, Endpoint)
        {
            Content = content
        };
        configureRequest?.Invoke(request);
        return await _client.SendAsync(request);
    }

    private static byte[] CreateWorkbook(IReadOnlyCollection<StudentImportRow> rows)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Students");

        for (int i = 0; i < BulkImportStudentWorkbookColumns.Headers.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = BulkImportStudentWorkbookColumns.Headers[i];
        }

        int rowNumber = 2;
        foreach (StudentImportRow row in rows)
        {
            WriteRow(sheet, rowNumber, row);
            rowNumber++;
        }

        return SaveWorkbook(workbook);
    }

    private static byte[] CreateWorkbookWithSampleMarker(StudentImportRow row)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Students");

        for (int i = 0; i < BulkImportStudentWorkbookColumns.Headers.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = BulkImportStudentWorkbookColumns.Headers[i];
        }

        sheet.Cell(1, BulkImportStudentWorkbookColumns.Headers.Count + 1).Value =
            BulkImportStudentWorkbookColumns.TemplateRowMarker;
        WriteRow(sheet, rowNumber: 2, row);
        sheet.Cell(2, BulkImportStudentWorkbookColumns.Headers.Count + 1).Value =
            BulkImportStudentWorkbookColumns.SampleRowMarker;

        return SaveWorkbook(workbook);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "scripts", "test-data")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing scripts/test-data.");
    }

    private static void WriteRow(IXLWorksheet sheet, int rowNumber, StudentImportRow row)
    {
        sheet.Cell(rowNumber, 1).Value = row.SchoolName;
        sheet.Cell(rowNumber, 2).Value = row.OrganizationId;
        sheet.Cell(rowNumber, 3).Value = row.IdentityNumber;
        sheet.Cell(rowNumber, 4).Value = row.FullName;
        sheet.Cell(rowNumber, 5).Value = row.DateOfBirth.ToDateTime(TimeOnly.MinValue);
        sheet.Cell(rowNumber, 6).Value = row.NationalityCode;
        sheet.Cell(rowNumber, 7).Value = row.CitizenshipStatusCode;
        sheet.Cell(rowNumber, 8).Value = row.StudentNumber;
        sheet.Cell(rowNumber, 9).Value = row.AcademicYear;
        sheet.Cell(rowNumber, 10).Value = row.LevelCode;
        sheet.Cell(rowNumber, 11).Value = row.ClassCode;
        sheet.Cell(rowNumber, 12).Value = row.StartDate?.ToDateTime(TimeOnly.MinValue);
        sheet.Cell(rowNumber, 13).Value = row.Email;
        sheet.Cell(rowNumber, 14).Value = row.Mobile;
        sheet.Cell(rowNumber, 15).Value = row.Address;
    }

    private static byte[] SaveWorkbook(XLWorkbook workbook)
    {
        using MemoryStream stream = new();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static StudentImportRow ValidRow(string suffix, string rowSuffix)
        => new(
            SchoolName: null,
            OrganizationId: null,
            IdentityNumber: ValidIdentityNumber(suffix, rowSuffix),
            FullName: $"UM015 Student {suffix} {rowSuffix}",
            DateOfBirth: new DateOnly(2008, 5, 12),
            NationalityCode: "SG",
            CitizenshipStatusCode: "CITIZEN",
            StudentNumber: $"UM015-{rowSuffix}-{suffix}",
            AcademicYear: "2026",
            LevelCode: "BACHELOR",
            ClassCode: "4A",
            StartDate: new DateOnly(2026, 1, 2),
            Email: $"um015.{suffix}.{rowSuffix}@example.com",
            Mobile: "+6591234567",
            Address: $"UM015 address {suffix}");

    private static async Task<BulkImportResponse> ReadBulkImportResponseAsync(HttpResponseMessage response)
    {
        using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(stream);
        JsonElement data = document.RootElement.GetProperty("data");
        return JsonSerializer.Deserialize<BulkImportResponse>(
            data.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static bool AnyAuditLogDetailsContaining(MoeDbContext db, string value)
    {
        Type entityType = typeof(Person).Assembly.GetType(
            "Moe.Modules.IdentityPlatform.Domain.Audit.AuditLog",
            throwOnError: true)!;
        IQueryable query = CreateQueryable(db, entityType);
        return query.Cast<object>().AsEnumerable().Any(x =>
            (string?)GetProperty(x, "ActionCode") == AuditActionCodes.StudentBulkImportCompleted
            && ((string?)GetProperty(x, "ChangedFieldsJson"))?.Contains(value, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static IQueryable CreateQueryable(MoeDbContext db, Type entityType)
    {
        var setMethod = typeof(DbContext)
            .GetMethods()
            .Single(x => x.Name == nameof(DbContext.Set)
                && x.IsGenericMethod
                && x.GetParameters().Length == 0);

        return (IQueryable)setMethod.MakeGenericMethod(entityType).Invoke(db, null)!;
    }

    private static object? GetProperty(object target, string propertyName)
    {
        return target.GetType().GetProperty(propertyName)!.GetValue(target);
    }

    private static string UniqueSuffix()
        => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static string ValidIdentityNumber(string suffix, string rowSuffix)
    {
        int number = StableSevenDigitNumber($"{suffix}-{rowSuffix}");
        string digits = number.ToString("D7");
        return $"S{digits}{ComputeChecksum('S', digits)}";
    }

    private static int StableSevenDigitNumber(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (byte item in Encoding.UTF8.GetBytes(value))
            {
                hash ^= item;
                hash *= 16777619;
            }

            return (int)(hash % 10_000_000);
        }
    }

    private static char ComputeChecksum(char prefix, string digits)
    {
        int[] weights = [2, 7, 6, 5, 4, 3, 2];
        int sum = prefix is 'T' or 'G' ? 4 : prefix is 'M' ? 3 : 0;
        for (int index = 0; index < weights.Length; index++)
        {
            sum += (digits[index] - '0') * weights[index];
        }

        string checksumTable = prefix switch
        {
            'S' or 'T' => "JZIHGFEDCBA",
            'F' or 'G' => "XWUTRQPNMLK",
            'M' => "XWUTRQPNJLK",
            _ => throw new ArgumentOutOfRangeException(nameof(prefix), prefix, null)
        };

        return checksumTable[sum % 11];
    }

    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync();
        Assert.Fail($"Expected {(int)expected} {expected}, got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    private sealed record StudentImportRow(
        string? SchoolName,
        long? OrganizationId,
        string IdentityNumber,
        string FullName,
        DateOnly DateOfBirth,
        string NationalityCode,
        string CitizenshipStatusCode,
        string StudentNumber,
        string AcademicYear,
        string LevelCode,
        string ClassCode,
        DateOnly? StartDate,
        string? Email,
        string? Mobile,
        string? Address);

    private sealed record BulkImportResponse(
        int TotalRows,
        int SucceededCount,
        int FailedCount,
        int SkippedCount,
        IReadOnlyList<BulkImportRowResult> Results);

    private sealed record BulkImportRowResult(
        int RowNumber,
        string Status,
        long? PersonId,
        string? ErrorCode,
        string? ErrorMessage);
}
