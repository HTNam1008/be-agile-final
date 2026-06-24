using FluentValidation;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.Students.BulkImportStudents;

internal sealed class BulkImportStudentsHandler(
    IStudentBulkImportWorkbookReader workbookReader,
    IValidator<CreateStudentCommand> validator,
    ICommandHandler<CreateStudentCommand, CreateStudentResponse> createStudentHandler)
    : ICommandHandler<BulkImportStudentsCommand, BulkImportStudentsResponse>
{
    private const int MaxRows = 1000;
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
        foreach (BulkImportStudentWorkbookRow row in rows)
        {
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

            results.Add(createResult.IsSuccess
                ? BulkImportStudentRowResult.Succeeded(row.RowNumber, createResult.Value.PersonId)
                : BulkImportStudentRowResult.Failed(
                    row.RowNumber,
                    createResult.Error.Code,
                    createResult.Error.Message));
        }

        int succeededCount = results.Count(x => x.Status == "Succeeded");
        BulkImportStudentsResponse response = new(
            rows.Count,
            succeededCount,
            rows.Count - succeededCount,
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
            row.Mobile,
            row.Address,
            IsAccountHolder: true);
}
