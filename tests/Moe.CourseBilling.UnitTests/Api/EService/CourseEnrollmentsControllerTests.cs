using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Api.EService;
using Moe.Modules.CourseBilling.Application.Enrollments.CourseContent;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Api.EService;

public sealed class CourseEnrollmentsControllerTests
{
    [Fact]
    public async Task DownloadMaterial_WhenStreamCanSeek_EnablesRangeProcessing()
    {
        MemoryStream content = new(new byte[] { 1, 2, 3 }, writable: false);
        CourseEnrollmentsController controller = CreateController(content);

        IActionResult result = await controller.DownloadMaterial(1, 2, "pdf", CancellationToken.None);

        FileStreamResult file = result.Should().BeOfType<FileStreamResult>().Subject;
        file.EnableRangeProcessing.Should().BeTrue();
        file.FileStream.Should().BeSameAs(content);
    }

    [Fact]
    public async Task DownloadMaterial_WhenStreamCannotSeek_DisablesRangeProcessing()
    {
        NonSeekableStream content = new(new MemoryStream(new byte[] { 1, 2, 3 }));
        CourseEnrollmentsController controller = CreateController(content);

        IActionResult result = await controller.DownloadMaterial(1, 2, "pdf", CancellationToken.None);

        FileStreamResult file = result.Should().BeOfType<FileStreamResult>().Subject;
        file.EnableRangeProcessing.Should().BeFalse();
    }

    private static CourseEnrollmentsController CreateController(Stream content)
        => new(
            new CommandDispatcherDouble(),
            new QueryDispatcherDouble(new StudentCourseMaterialDownload(
                content,
                "application/pdf",
                "slide.pdf")),
            NullLogger<CourseEnrollmentsController>.Instance);

    private sealed class QueryDispatcherDouble(StudentCourseMaterialDownload download) : IQueryDispatcher
    {
        public Task<Result<TResponse>> Send<TResponse>(
            IQuery<TResponse> query,
            CancellationToken cancellationToken)
            => Task.FromResult(Result<TResponse>.Success((TResponse)(object)download));
    }

    private sealed class CommandDispatcherDouble : ICommandDispatcher
    {
        public Task<Result<TResponse>> Send<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<Result> Send(ICommand command, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
            => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
