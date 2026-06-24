using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class StudentBulkImportApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Endpoint = "/api/admin/v1/students/bulk-import";
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task BulkImport_WithValidWorkbook_CreatesStudentsWithoutEducationAccountsOrImportAudit()
    {
        string suffix = UniqueSuffix();
        StudentImportRow[] rows =
        [
            ValidRow(suffix, "001"),
            ValidRow(suffix, "002")
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
        Assert.False(AnyAuditLogContaining(db, suffix));
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
        using MultipartFormDataContent content = new();
        ByteArrayContent fileContent = new(workbook);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "students.xlsx");
        return await _client.PostAsync(Endpoint, content);
    }

    private static byte[] CreateWorkbook(IReadOnlyCollection<StudentImportRow> rows)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Students");
        string[] headers =
        [
            "SchoolName",
            "OrganizationId",
            "IdentityNumber",
            "FullName",
            "DateOfBirth",
            "NationalityCode",
            "CitizenshipStatusCode",
            "StudentNumber",
            "AcademicYear",
            "LevelCode",
            "ClassCode",
            "StartDate",
            "Email",
            "Mobile",
            "Address"
        ];

        for (int i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        int rowNumber = 2;
        foreach (StudentImportRow row in rows)
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
            rowNumber++;
        }

        using MemoryStream stream = new();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static StudentImportRow ValidRow(string suffix, string rowSuffix)
        => new(
            SchoolName: null,
            OrganizationId: null,
            IdentityNumber: $"U{suffix[..7]}{IdentityFragment(rowSuffix)}",
            FullName: $"UM015 Student {suffix} {rowSuffix}",
            DateOfBirth: new DateOnly(2008, 5, 12),
            NationalityCode: "SG",
            CitizenshipStatusCode: "CITIZEN",
            StudentNumber: $"UM015-{rowSuffix}-{suffix}",
            AcademicYear: "2026",
            LevelCode: "SEC_4",
            ClassCode: "4A",
            StartDate: new DateOnly(2026, 1, 2),
            Email: $"um015.{suffix}.{rowSuffix}@example.com",
            Mobile: "+6591234567",
            Address: $"UM015 address {suffix}");

    private static string IdentityFragment(string value)
        => new(value
            .Where(char.IsLetterOrDigit)
            .Take(12)
            .ToArray());

    private static async Task<BulkImportResponse> ReadBulkImportResponseAsync(HttpResponseMessage response)
    {
        using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(stream);
        JsonElement data = document.RootElement.GetProperty("data");
        return JsonSerializer.Deserialize<BulkImportResponse>(
            data.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static bool AnyAuditLogContaining(MoeDbContext db, string value)
    {
        Type entityType = typeof(Person).Assembly.GetType(
            "Moe.Modules.IdentityPlatform.Domain.Audit.AuditLog",
            throwOnError: true)!;
        IQueryable query = CreateQueryable(db, entityType);
        return query.Cast<object>().AsEnumerable().Any(x =>
            ((string?)GetProperty(x, "ActionCode"))?.Contains("IMPORT", StringComparison.OrdinalIgnoreCase) == true
            || ((string?)GetProperty(x, "ChangedFieldsJson"))?.Contains(value, StringComparison.OrdinalIgnoreCase) == true);
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
        IReadOnlyList<BulkImportRowResult> Results);

    private sealed record BulkImportRowResult(
        int RowNumber,
        string Status,
        long? PersonId,
        string? ErrorCode,
        string? ErrorMessage);
}
