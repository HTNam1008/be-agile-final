using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Storage;
using StackExchange.Redis;

namespace Moe.Modules.CourseBilling.Infrastructure.Storage;

internal sealed class RedisCourseMaterialPreviewCache(
    IConfiguration configuration,
    ILogger<RedisCourseMaterialPreviewCache> logger) : ICourseMaterialPreviewCache
{
    private readonly string? _connectionString = configuration["Redis:ConnectionString"];
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(
        Math.Max(1, configuration.GetValue("Redis:CourseMaterialPreviewTtlMinutes", 10080)));
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private ConnectionMultiplexer? _connection;

    public async Task<Stream?> GetPdfAsync(CourseMaterial material, CancellationToken cancellationToken)
    {
        IDatabase? database = await GetDatabaseAsync(cancellationToken);
        if (database is null)
            return null;

        try
        {
            RedisValue value = await database.StringGetAsync(BuildKey(material));
            return value.HasValue
                ? new MemoryStream((byte[])value!, writable: false)
                : null;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Could not read course material preview cache for material {CourseMaterialId}.", material.Id);
            return null;
        }
    }

    public async Task SetPdfAsync(CourseMaterial material, byte[] pdfBytes, CancellationToken cancellationToken)
    {
        if (pdfBytes.Length == 0)
            return;

        IDatabase? database = await GetDatabaseAsync(cancellationToken);
        if (database is null)
            return;

        try
        {
            await database.StringSetAsync(BuildKey(material), pdfBytes, _ttl);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Could not write course material preview cache for material {CourseMaterialId}.", material.Id);
        }
    }

    private async Task<IDatabase?> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return null;

        if (_connection is { IsConnected: true })
            return _connection.GetDatabase();

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsConnected: true })
                return _connection.GetDatabase();

            _connection?.Dispose();
            _connection = await ConnectionMultiplexer.ConnectAsync(_connectionString);
            return _connection.GetDatabase();
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Could not connect to Redis course material preview cache.");
            return null;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private static string BuildKey(CourseMaterial material)
        => $"course-material-preview:{material.Id}:{Hash(material.StoragePath)}:pdf";

    private static string Hash(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
    }
}
