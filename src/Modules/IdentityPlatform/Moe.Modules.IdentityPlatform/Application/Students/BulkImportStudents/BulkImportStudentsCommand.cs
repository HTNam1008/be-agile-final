using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.Students.BulkImportStudents;

public sealed record BulkImportStudentsCommand(
    Stream WorkbookStream,
    string FileName) : ICommand<BulkImportStudentsResponse>;
