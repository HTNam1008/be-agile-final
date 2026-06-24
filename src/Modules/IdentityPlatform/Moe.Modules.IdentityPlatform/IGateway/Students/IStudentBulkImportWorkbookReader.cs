using Moe.Modules.IdentityPlatform.Application.Students.BulkImportStudents;

namespace Moe.Modules.IdentityPlatform.IGateway.Students;

public interface IStudentBulkImportWorkbookReader
{
    Task<IReadOnlyList<BulkImportStudentWorkbookRow>> ReadAsync(
        Stream workbookStream,
        CancellationToken cancellationToken);
}
