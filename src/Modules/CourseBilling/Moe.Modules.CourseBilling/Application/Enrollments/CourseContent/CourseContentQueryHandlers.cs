using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
    ICourseMaterialPreviewConverter previewConverter,
    ICourseMaterialPreviewCache previewCache,
    ILogger<DownloadStudentCourseMaterialHandler> logger,
    IClock clock) : IQueryHandler<DownloadStudentCourseMaterialQuery, StudentCourseMaterialDownload>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PreviewLocks = new();

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

        if (query.PreviewAsPdf)
        {
            if (IsPdfMaterial(material))
            {
                Stream pdfContent = await storage.OpenReadAsync(material.StoragePath, cancellationToken);
                return Result<StudentCourseMaterialDownload>.Success(new(
                    pdfContent,
                    "application/pdf",
                    Path.ChangeExtension(material.OriginalFileName, ".pdf")));
            }

            Stopwatch previewStopwatch = Stopwatch.StartNew();
            Stream? cachedPreview = await previewCache.GetPdfAsync(material, cancellationToken);
            if (cachedPreview is not null)
            {
                logger.LogInformation(
                    "Course material PDF preview served from cache. CourseMaterialId={CourseMaterialId}, ElapsedMs={ElapsedMs}.",
                    material.Id,
                    previewStopwatch.ElapsedMilliseconds);
                return Result<StudentCourseMaterialDownload>.Success(new(
                    cachedPreview,
                    "application/pdf",
                    Path.ChangeExtension(material.OriginalFileName, ".pdf")));
            }

            string lockKey = $"{material.Id}:{material.StoragePath}";
            SemaphoreSlim previewLock = PreviewLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            await previewLock.WaitAsync(cancellationToken);
            try
            {
                cachedPreview = await previewCache.GetPdfAsync(material, cancellationToken);
                if (cachedPreview is not null)
                {
                    logger.LogInformation(
                        "Course material PDF preview served from cache after wait. CourseMaterialId={CourseMaterialId}, ElapsedMs={ElapsedMs}.",
                        material.Id,
                        previewStopwatch.ElapsedMilliseconds);
                    return Result<StudentCourseMaterialDownload>.Success(new(
                        cachedPreview,
                        "application/pdf",
                        Path.ChangeExtension(material.OriginalFileName, ".pdf")));
                }

                logger.LogInformation(
                    "Course material PDF preview converting. CourseMaterialId={CourseMaterialId}, FileName={FileName}, ContentType={ContentType}.",
                    material.Id,
                    material.OriginalFileName,
                    material.ContentType);

                await using Stream sourceContent = await storage.OpenReadAsync(material.StoragePath, cancellationToken);
                Stream? preview = await previewConverter.TryConvertToPdfAsync(
                    material.OriginalFileName,
                    material.ContentType,
                    sourceContent,
                    cancellationToken);

                if (preview is null)
                {
                    logger.LogWarning(
                        "PDF preview converter returned no preview for course material {CourseMaterialId}. FileName={FileName}, ContentType={ContentType}.",
                        material.Id,
                        material.OriginalFileName,
                        material.ContentType);
                    return Result<StudentCourseMaterialDownload>.Failure(CourseErrors.MaterialPreviewUnavailable);
                }

                await using (preview)
                {
                    byte[] previewBytes = await ReadAllBytesAsync(preview, cancellationToken);
                    await previewCache.SetPdfAsync(material, previewBytes, cancellationToken);
                    logger.LogInformation(
                        "Course material PDF preview converted. CourseMaterialId={CourseMaterialId}, SizeBytes={SizeBytes}, ElapsedMs={ElapsedMs}.",
                        material.Id,
                        previewBytes.Length,
                        previewStopwatch.ElapsedMilliseconds);

                    return Result<StudentCourseMaterialDownload>.Success(new(
                        new MemoryStream(previewBytes, writable: false),
                        "application/pdf",
                        Path.ChangeExtension(material.OriginalFileName, ".pdf")));
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Could not create PDF preview for course material {CourseMaterialId}. FileName={FileName}, ContentType={ContentType}.",
                    material.Id,
                    material.OriginalFileName,
                    material.ContentType);
                return Result<StudentCourseMaterialDownload>.Failure(CourseErrors.MaterialPreviewUnavailable);
            }
            finally
            {
                previewLock.Release();
            }
        }

        Stream originalContent = await storage.OpenReadAsync(material.StoragePath, cancellationToken);
        return Result<StudentCourseMaterialDownload>.Success(new(
            originalContent,
            material.ContentType,
            material.OriginalFileName));
    }

    private static bool IsPdfMaterial(CourseMaterial material)
        => material.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
           || Path.GetExtension(material.OriginalFileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using MemoryStream memory = new();
        await stream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }
}

internal sealed class GetStudentCourseMaterialOfficePreviewHandler(
    ICurrentUser currentUser,
    IStudentCourseContentRepository contents,
    ICourseMaterialStorageService storage,
    IClock clock)
    : IQueryHandler<GetStudentCourseMaterialOfficePreviewQuery, StudentCourseMaterialOfficePreviewResponse>
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(10);
    private const string OfficeViewerUrl = "https://view.officeapps.live.com/op/embed.aspx?src=";

    public async Task<Result<StudentCourseMaterialOfficePreviewResponse>> Handle(
        GetStudentCourseMaterialOfficePreviewQuery query,
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
            return Result<StudentCourseMaterialOfficePreviewResponse>.Failure(snapshotResult.Error);

        CourseMaterial? material = snapshotResult.Value.Materials
            .SingleOrDefault(item => item.Id == query.CourseMaterialId);
        if (material is null)
            return Result<StudentCourseMaterialOfficePreviewResponse>.Failure(CourseErrors.MaterialNotFound);
        if (!IsOfficeWebPowerPointMaterial(material))
            return Result<StudentCourseMaterialOfficePreviewResponse>.Failure(CourseErrors.MaterialPreviewUnavailable);

        DateTimeOffset expiresAtUtc = clock.UtcNow.Add(PreviewLifetime);
        Uri? readUri = await storage.CreateReadUriAsync(
            material.StoragePath,
            expiresAtUtc,
            cancellationToken);
        if (readUri is null)
            return Result<StudentCourseMaterialOfficePreviewResponse>.Failure(CourseErrors.MaterialPreviewUnavailable);

        string previewUrl = OfficeViewerUrl + Uri.EscapeDataString(readUri.AbsoluteUri) + "&ui=en-US&rs=en-US";
        return Result<StudentCourseMaterialOfficePreviewResponse>.Success(new(
            previewUrl,
            expiresAtUtc));
    }

    private static bool IsOfficeWebPowerPointMaterial(CourseMaterial material)
    {
        string extension = Path.GetExtension(material.OriginalFileName).ToLowerInvariant();
        return extension is ".pptx" or ".ppsx"
               || material.ContentType.Contains("presentationml", StringComparison.OrdinalIgnoreCase);
    }
}
