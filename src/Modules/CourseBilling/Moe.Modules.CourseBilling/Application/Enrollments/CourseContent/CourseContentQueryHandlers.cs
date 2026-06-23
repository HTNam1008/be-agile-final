using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Contracts.Enrollments;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.IGateway.Storage;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Enrollments.CourseContent;

internal sealed class GetStudentCourseContentHandler(
    ICurrentUser currentUser,
    IStudentCourseContentRepository contents,
    IClock clock) : IQueryHandler<GetStudentCourseContentQuery, StudentCourseContentResponse>
{
    public async Task<Result<StudentCourseContentResponse>> Handle(
        GetStudentCourseContentQuery query,
        CancellationToken cancellationToken)
    {
        Result<StudentCourseContentSnapshot> snapshotResult = await LoadAccessibleSnapshotAsync(
            currentUser,
            contents,
            clock,
            query.EnrollmentId,
            cancellationToken);
        if (snapshotResult.IsFailure)
            return Result<StudentCourseContentResponse>.Failure(snapshotResult.Error);

        StudentCourseContentSnapshot snapshot = snapshotResult.Value;
        return Result<StudentCourseContentResponse>.Success(new StudentCourseContentResponse(
            snapshot.Enrollment.Id,
            snapshot.Course.Id,
            snapshot.Course.CourseCode,
            snapshot.Course.CourseName,
            snapshot.Course.StartDate,
            snapshot.Course.EndDate,
            snapshot.Materials.Select(ToResponse).ToArray()));
    }

    internal static async Task<Result<StudentCourseContentSnapshot>> LoadAccessibleSnapshotAsync(
        ICurrentUser currentUser,
        IStudentCourseContentRepository contents,
        IClock clock,
        long enrollmentId,
        CancellationToken cancellationToken)
    {
        if (currentUser.PersonId is not long personId)
            return Result<StudentCourseContentSnapshot>.Failure(CourseBillingErrors.StudentIdentityRequired);

        StudentCourseContentSnapshot? snapshot = await contents.FindAsync(
            enrollmentId,
            personId,
            cancellationToken);
        if (snapshot is null)
            return Result<StudentCourseContentSnapshot>.Failure(CourseErrors.EnrollmentNotFound);

        Result access = CourseContentAccessPolicy.Check(
            snapshot.Enrollment,
            snapshot.Course,
            DateOnly.FromDateTime(clock.UtcNow.UtcDateTime));
        return access.IsFailure
            ? Result<StudentCourseContentSnapshot>.Failure(access.Error)
            : Result<StudentCourseContentSnapshot>.Success(snapshot);
    }

    private static StudentCourseMaterialResponse ToResponse(CourseMaterial material)
        => new(
            material.Id,
            material.MaterialTitle,
            material.MaterialDescription,
            material.MaterialTypeCode,
            material.OriginalFileName,
            material.ContentType,
            material.FileSizeBytes,
            material.DisplayOrder,
            material.IsRequired);
}

internal sealed class DownloadStudentCourseMaterialHandler(
    ICurrentUser currentUser,
    IStudentCourseContentRepository contents,
    ICourseMaterialStorageService storage,
    IClock clock) : IQueryHandler<DownloadStudentCourseMaterialQuery, StudentCourseMaterialDownload>
{
    public async Task<Result<StudentCourseMaterialDownload>> Handle(
        DownloadStudentCourseMaterialQuery query,
        CancellationToken cancellationToken)
    {
        Result<StudentCourseContentSnapshot> snapshotResult =
            await GetStudentCourseContentHandler.LoadAccessibleSnapshotAsync(
                currentUser,
                contents,
                clock,
                query.EnrollmentId,
                cancellationToken);
        if (snapshotResult.IsFailure)
            return Result<StudentCourseMaterialDownload>.Failure(snapshotResult.Error);

        CourseMaterial? material = snapshotResult.Value.Materials
            .SingleOrDefault(x => x.Id == query.CourseMaterialId);
        if (material is null)
            return Result<StudentCourseMaterialDownload>.Failure(CourseErrors.MaterialNotFound);

        Stream content = await storage.OpenReadAsync(material.StoragePath, cancellationToken);
        return Result<StudentCourseMaterialDownload>.Success(new(
            content,
            material.ContentType,
            material.OriginalFileName));
    }
}
