using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.Infrastructure.Storage;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Infrastructure.Storage;

public sealed class CompositeCourseMaterialPreviewCacheTests
{
    [Fact]
    public async Task GetPdfAsync_WhenRedisHits_DoesNotReadBlob()
    {
        CourseMaterial material = CreateMaterial();
        PreviewCacheDouble redis = new(new byte[] { 1, 2, 3 });
        PreviewCacheDouble blob = new(null);
        CompositeCourseMaterialPreviewCache cache = CreateCache(redis, blob);

        await using Stream? result = await cache.GetPdfAsync(material, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Length.Should().Be(3);
        redis.GetCalls.Should().Be(1);
        blob.GetCalls.Should().Be(0);
    }

    [Fact]
    public async Task GetPdfAsync_WhenBlobHits_WarmsRedis()
    {
        CourseMaterial material = CreateMaterial();
        PreviewCacheDouble redis = new(null);
        PreviewCacheDouble blob = new(new byte[] { 4, 5, 6, 7 });
        CompositeCourseMaterialPreviewCache cache = CreateCache(redis, blob);

        await using Stream? result = await cache.GetPdfAsync(material, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Length.Should().Be(4);
        redis.SetCalls.Should().Be(1);
        redis.LastSetBytes.Should().Equal(4, 5, 6, 7);
    }

    [Fact]
    public async Task GetPdfAsync_WhenBothMiss_ReturnsNull()
    {
        PreviewCacheDouble redis = new(null);
        PreviewCacheDouble blob = new(null);
        CompositeCourseMaterialPreviewCache cache = CreateCache(redis, blob);

        Stream? result = await cache.GetPdfAsync(CreateMaterial(), CancellationToken.None);

        result.Should().BeNull();
        redis.SetCalls.Should().Be(0);
    }

    [Fact]
    public async Task SetPdfAsync_WritesRedisAndBlob()
    {
        PreviewCacheDouble redis = new(null);
        PreviewCacheDouble blob = new(null);
        CompositeCourseMaterialPreviewCache cache = CreateCache(redis, blob);

        await cache.SetPdfAsync(CreateMaterial(), new byte[] { 8, 9 }, CancellationToken.None);

        redis.SetCalls.Should().Be(1);
        blob.SetCalls.Should().Be(1);
        redis.LastSetBytes.Should().Equal(8, 9);
        blob.LastSetBytes.Should().Equal(8, 9);
    }

    [Fact]
    public async Task GetPdfAsync_WhenRedisReadFails_FallsBackToBlob()
    {
        PreviewCacheDouble redis = new(null) { ThrowOnGet = true };
        PreviewCacheDouble blob = new(new byte[] { 10, 11 });
        CompositeCourseMaterialPreviewCache cache = CreateCache(redis, blob);

        await using Stream? result = await cache.GetPdfAsync(CreateMaterial(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Length.Should().Be(2);
        blob.GetCalls.Should().Be(1);
    }

    [Fact]
    public async Task SetPdfAsync_WhenCacheWriteFails_DoesNotThrow()
    {
        PreviewCacheDouble redis = new(null) { ThrowOnSet = true };
        PreviewCacheDouble blob = new(null) { ThrowOnSet = true };
        CompositeCourseMaterialPreviewCache cache = CreateCache(redis, blob);

        Func<Task> act = () => cache.SetPdfAsync(CreateMaterial(), new byte[] { 12 }, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static CompositeCourseMaterialPreviewCache CreateCache(
        PreviewCacheDouble redis,
        PreviewCacheDouble blob)
        => new(
            redis,
            blob,
            NullLogger<CompositeCourseMaterialPreviewCache>.Instance);

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

    private sealed class PreviewCacheDouble(byte[]? bytes) :
        ICourseMaterialPreviewRedisCache,
        ICourseMaterialPreviewBlobCache
    {
        public int GetCalls { get; private set; }
        public int SetCalls { get; private set; }
        public byte[]? LastSetBytes { get; private set; }
        public bool ThrowOnGet { get; init; }
        public bool ThrowOnSet { get; init; }

        public Task<Stream?> GetPdfAsync(CourseMaterial material, CancellationToken cancellationToken)
        {
            GetCalls++;
            if (ThrowOnGet)
                throw new InvalidOperationException("Cache read failed.");
            return Task.FromResult<Stream?>(bytes is null ? null : new MemoryStream(bytes, writable: false));
        }

        public Task SetPdfAsync(CourseMaterial material, byte[] pdfBytes, CancellationToken cancellationToken)
        {
            SetCalls++;
            if (ThrowOnSet)
                throw new InvalidOperationException("Cache write failed.");
            LastSetBytes = pdfBytes;
            return Task.CompletedTask;
        }
    }
}
