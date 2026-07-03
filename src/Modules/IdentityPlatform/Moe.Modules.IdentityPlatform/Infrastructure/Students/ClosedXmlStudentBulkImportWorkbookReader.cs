using ClosedXML.Excel;
using Moe.Modules.IdentityPlatform.Application.Students.BulkImportStudents;
using Moe.Modules.IdentityPlatform.IGateway.Students;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Students;

internal sealed class ClosedXmlStudentBulkImportWorkbookReader : IStudentBulkImportWorkbookReader
{
    public Task<IReadOnlyList<BulkImportStudentWorkbookRow>> ReadAsync(
        Stream workbookStream,
        CancellationToken cancellationToken)
    {
        using XLWorkbook workbook = new(workbookStream);
        IXLWorksheet sheet = workbook.Worksheets.First();
        IXLRange? usedRange = sheet.RangeUsed();
        if (usedRange is null)
        {
            return Task.FromResult<IReadOnlyList<BulkImportStudentWorkbookRow>>([]);
        }

        Dictionary<string, int> columns = BuildColumnMap(usedRange.FirstRowUsed());
        List<BulkImportStudentWorkbookRow> rows = [];
        foreach (IXLRangeRow row in usedRange.RowsUsed().Skip(1))
        {
            if (IsBlank(row, columns.Values))
            {
                continue;
            }

            bool isTemplateSampleRow = IsTemplateSampleRow(row, columns);

            rows.Add(new BulkImportStudentWorkbookRow(
                row.RowNumber(),
                Text(row, columns, BulkImportStudentWorkbookColumns.SchoolName),
                Long(row, columns, BulkImportStudentWorkbookColumns.OrganizationId),
                Text(row, columns, BulkImportStudentWorkbookColumns.MockPassPersonId),
                Text(row, columns, BulkImportStudentWorkbookColumns.IdentityNumber) ?? string.Empty,
                Text(row, columns, BulkImportStudentWorkbookColumns.FullName) ?? string.Empty,
                Date(row, columns, BulkImportStudentWorkbookColumns.DateOfBirth) ?? DateOnly.MinValue,
                Text(row, columns, BulkImportStudentWorkbookColumns.NationalityCode) ?? string.Empty,
                Text(row, columns, BulkImportStudentWorkbookColumns.CitizenshipStatusCode),
                Text(row, columns, BulkImportStudentWorkbookColumns.StudentNumber) ?? string.Empty,
                Text(row, columns, BulkImportStudentWorkbookColumns.AcademicYear) ?? string.Empty,
                Text(row, columns, BulkImportStudentWorkbookColumns.LevelCode) ?? string.Empty,
                Text(row, columns, BulkImportStudentWorkbookColumns.ClassCode) ?? string.Empty,
                Date(row, columns, BulkImportStudentWorkbookColumns.StartDate),
                Text(row, columns, BulkImportStudentWorkbookColumns.Email),
                Text(row, columns, BulkImportStudentWorkbookColumns.ContactNumber),
                Text(row, columns, BulkImportStudentWorkbookColumns.Address),
                isTemplateSampleRow));
        }

        return Task.FromResult<IReadOnlyList<BulkImportStudentWorkbookRow>>(rows);
    }

    private static Dictionary<string, int> BuildColumnMap(IXLRangeRow headerRow)
    {
        return headerRow.CellsUsed()
            .Select(cell => new
            {
                Name = cell.GetString().Trim(),
                cell.Address.ColumnNumber
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().ColumnNumber, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsBlank(IXLRangeRow row, IEnumerable<int> columnNumbers)
        => columnNumbers.All(columnNumber => row.Cell(columnNumber).IsEmpty());

    private static bool IsTemplateSampleRow(IXLRangeRow row, IReadOnlyDictionary<string, int> columns)
        => columns.TryGetValue(BulkImportStudentWorkbookColumns.TemplateRowMarker, out int columnNumber)
            && string.Equals(
                row.Cell(columnNumber).GetString().Trim(),
                BulkImportStudentWorkbookColumns.SampleRowMarker,
                StringComparison.OrdinalIgnoreCase);

    private static string? Text(IXLRangeRow row, IReadOnlyDictionary<string, int> columns, string name)
    {
        if (!columns.TryGetValue(name, out int columnNumber))
        {
            return null;
        }

        string value = row.Cell(columnNumber).GetString().Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static long? Long(IXLRangeRow row, IReadOnlyDictionary<string, int> columns, string name)
    {
        string? text = Text(row, columns, name);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return long.TryParse(text, out long value) ? value : null;
    }

    private static DateOnly? Date(IXLRangeRow row, IReadOnlyDictionary<string, int> columns, string name)
    {
        if (!columns.TryGetValue(name, out int columnNumber))
        {
            return null;
        }

        IXLCell cell = row.Cell(columnNumber);
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue(out DateTime dateTime))
        {
            return DateOnly.FromDateTime(dateTime);
        }

        string value = cell.GetString().Trim();
        return DateOnly.TryParse(value, out DateOnly date) ? date : null;
    }
}
