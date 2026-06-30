using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Application.Enrollments.CourseContent;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.IGateway.Storage;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Application.Enrollments.CourseContent;

public sealed class DownloadStudentCourseMaterialHandlerTests
{
    [Fact]
    public async Task Handle_WhenConcurrentPreviewRequestsMissCache_ConvertsOnlyOnce()
    {
        CourseMaterial material = CreateMaterial();
        StudentCourseContentSnapshot snapshot = CreateSnapshot(material);
        StudentCourseContentRepositoryDouble contents = new(snapshot);
        PreviewCacheDouble cache = new();
        PreviewConverterDouble converter = new();
        DownloadStudentCourseMaterialHandler handler = new(
            new CurrentUserDouble(),
            contents,
            new CourseMaterialStorageServiceDouble(),
            converter,
            cache,
            NullLogger<DownloadStudentCourseMaterialHandler>.Instance,
            new ClockDouble());

        Task first = handler.Handle(
            new DownloadStudentCourseMaterialQuery(1, material.Id, PreviewAsPdf: true),
            CancellationToken.None);
        await converter.WaitUntilEnteredAsync();

        Task second = handler.Handle(
            new DownloadStudentCourseMaterialQuery(1, material.Id, PreviewAsPdf: true),
            CancellationToken.None);
        converter.Release();

        await Task.WhenAll(first, second);

        converter.ConvertCalls.Should().Be(1);
        cache.SetCalls.Should().Be(1);
    }

    private static StudentCourseContentSnapshot CreateSnapshot(CourseMaterial material)
    {
        DateTime now = DateTime.UtcNow;
        Course course = new(
            1,
            "C-001",
            "Course",
            null,
            DateOnly.FromDateTime(now.AddDays(-1)),
            DateOnly.FromDateTime(now.AddDays(1)),
            now.AddDays(-10),
            now.AddDays(-2),
            1,
            now);
        CourseEnrollment enrollment = CourseEnrollment.JoinSelf(
            11,
            1,
            1,
            99,
            now,
            100,
            50).Value;
        enrollment.GrantPaidAccess(paidInFull: true);
        return new StudentCourseContentSnapshot(enrollment, course, [material]);
    }

    private static CourseMaterial CreateMaterial()
        => new(
            1,
            "Slide",
            null,
            "READING_MATERIAL",
            "slide.ppt",
            "slide.ppt",
            ".ppt",
            "application/vnd.ms-powerpoint",
            1024,
            "AZURE_BLOB",
            "courses/1/materials/slide.ppt",
            null,
            1,
            true,
            DateTime.UtcNow);

    private sealed class StudentCourseContentRepositoryDouble(StudentCourseContentSnapshot snapshot)
        : IStudentCourseContentRepository
    {
        public Task<StudentCourseContentSnapshot?> FindAsync(
            long enrollmentId,
            long personId,
            CancellationToken cancellationToken)
            => Task.FromResult<StudentCourseContentSnapshot?>(snapshot);
    }

    private sealed class CourseMaterialStorageServiceDouble : ICourseMaterialStorageService
    {
        public Task<StoredCourseMaterialFile> SaveAsync(
            long courseId,
            string originalFileName,
            string contentType,
            Stream content,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
            => Task.FromResult<Stream>(new MemoryStream(new byte[] { 1, 2, 3 }));

        public Task<Uri?> CreateReadUriAsync(
            string storagePath,
            DateTimeOffset expiresAtUtc,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class PreviewConverterDouble : ICourseMaterialPreviewConverter
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ConvertCalls { get; private set; }

        public async Task<Stream?> TryConvertToPdfAsync(
            string originalFileName,
            string contentType,
            Stream content,
            CancellationToken cancellationToken)
        {
            ConvertCalls++;
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return new MemoryStream(new byte[] { 4, 5, 6 });
        }

        public Task WaitUntilEnteredAsync() => _entered.Task;

        public void Release() => _release.TrySetResult();
    }

    private sealed class PreviewCacheDouble : ICourseMaterialPreviewCache
    {
        private byte[]? _bytes;
        public int SetCalls { get; private set; }

        public Task<Stream?> GetPdfAsync(CourseMaterial material, CancellationToken cancellationToken)
            => Task.FromResult<Stream?>(_bytes is null ? null : new MemoryStream(_bytes, writable: false));

        public Task SetPdfAsync(CourseMaterial material, byte[] pdfBytes, CancellationToken cancellationToken)
        {
            SetCalls++;
            _bytes = pdfBytes;
            return Task.CompletedTask;
        }
    }

    private sealed class CurrentUserDouble : ICurrentUser
    {
        public long? UserAccountId => 99;
        public long? PersonId => 11;
        public long? OrganizationUnitId => 1;
        public IReadOnlyCollection<long> OrganizationUnitIds => [1];
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> Permissions => [];
        public string Portal => "ESERVICE";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => true;
    }

    private sealed class ClockDouble : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
