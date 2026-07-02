using FluentValidation;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.Students.BulkImportStudents;

internal sealed class BulkImportStudentsHandler(
    IStudentBulkImportWorkbookReader workbookReader,
    IValidator<CreateStudentCommand> validator,
    ICommandHandler<CreateStudentCommand, CreateStudentResponse> createStudentHandler,
    IAuditService audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<BulkImportStudentsCommand, BulkImportStudentsResponse>
{
    private const int MaxRows = 1000;
    private const string SampleRowSkipped = "BULK_IMPORT.SAMPLE_ROW_SKIPPED";
    private const string RowValidationFailed = "BULK_IMPORT.ROW_VALIDATION_FAILED";
    private const string RowLimitExceeded = "BULK_IMPORT.ROW_LIMIT_EXCEEDED";

    public async Task<Result<BulkImportStudentsResponse>> Handle(
        BulkImportStudentsCommand command,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<BulkImportStudentWorkbookRow> rows =
            await workbookReader.ReadAsync(command.WorkbookStream, cancellationToken);

        if (rows.Count > MaxRows)
        {
            return Result<BulkImportStudentsResponse>.Failure(new Error(
                RowLimitExceeded,
                $"The uploaded workbook contains {rows.Count} data rows. The maximum allowed is {MaxRows}."));
        }

        List<BulkImportStudentRowResult> results = [];
        List<CreateStudentResponse> createdStudents = [];
        foreach (BulkImportStudentWorkbookRow row in rows)
        {
            if (row.IsTemplateSampleRow)
            {
                results.Add(BulkImportStudentRowResult.Skipped(
                    row.RowNumber,
                    SampleRowSkipped,
                    "This row was skipped because TemplateRow is SAMPLE_DO_NOT_IMPORT. Clear that marker before importing real student data."));
                continue;
            }

            CreateStudentCommand createCommand = ToCreateStudentCommand(row);
            var validation = await validator.ValidateAsync(createCommand, cancellationToken);
            if (!validation.IsValid)
            {
                results.Add(BulkImportStudentRowResult.Failed(
                    row.RowNumber,
                    RowValidationFailed,
                    string.Join("; ", validation.Errors.Select(x => x.ErrorMessage).Distinct())));
                continue;
            }

            Result<CreateStudentResponse> createResult =
                await createStudentHandler.Handle(createCommand, cancellationToken);

            if (createResult.IsSuccess)
            {
                createdStudents.Add(createResult.Value);
            }

            results.Add(createResult.IsSuccess
                ? BulkImportStudentRowResult.Succeeded(row.RowNumber, createResult.Value.PersonId)
                : BulkImportStudentRowResult.Failed(
                    row.RowNumber,
                    createResult.Error.Code,
                    createResult.Error.Message));
        }

        int succeededCount = results.Count(x => x.Status == "Succeeded");
        int skippedCount = results.Count(x => x.Status == "Skipped");
        foreach (var schoolGroup in createdStudents.GroupBy(x => x.OrganizationId))
        {
            await audit.RecordSchoolActionAsync(
                new SchoolAuditContext(
                    AuditActionCodes.StudentBulkImportCompleted,
                    "BulkImportStudents",
                    schoolGroup.Key,
                    schoolGroup.Key,
                    new SchoolAuditDetails(
                        "Bulk student import completed",
                        Count: schoolGroup.Count())),
                cancellationToken);
        }

        if (createdStudents.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        BulkImportStudentsResponse response = new(
            rows.Count,
            succeededCount,
            rows.Count - succeededCount - skippedCount,
            skippedCount,
            results);

        return Result<BulkImportStudentsResponse>.Success(response);
    }

    private static CreateStudentCommand ToCreateStudentCommand(BulkImportStudentWorkbookRow row)
        => new(
            row.SchoolName,
            row.OrganizationId,
            row.IdentityNumber,
            row.FullName,
            row.DateOfBirth,
            row.NationalityCode,
            row.CitizenshipStatusCode,
            row.StudentNumber,
            row.AcademicYear,
            row.LevelCode,
            row.ClassCode,
            row.StartDate,
            row.Email,
            row.ContactNumber,
            row.Address,
            IsAccountHolder: true);
}
