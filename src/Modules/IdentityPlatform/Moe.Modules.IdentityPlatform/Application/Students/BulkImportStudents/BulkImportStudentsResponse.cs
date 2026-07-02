namespace Moe.Modules.IdentityPlatform.Application.Students.BulkImportStudents;

public sealed record BulkImportStudentsResponse(
    int TotalRows,
    int SucceededCount,
    int FailedCount,
    int SkippedCount,
    IReadOnlyList<BulkImportStudentRowResult> Results);

public sealed record BulkImportStudentRowResult(
    int RowNumber,
    string Status,
    long? PersonId,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static BulkImportStudentRowResult Succeeded(int rowNumber, long personId)
        => new(rowNumber, "Succeeded", personId, null, null);

    public static BulkImportStudentRowResult Failed(int rowNumber, string errorCode, string errorMessage)
        => new(rowNumber, "Failed", null, errorCode, errorMessage);

    public static BulkImportStudentRowResult Skipped(int rowNumber, string errorCode, string message)
        => new(rowNumber, "Skipped", null, errorCode, message);
}
