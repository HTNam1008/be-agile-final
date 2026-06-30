using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure;
using Microsoft.Extensions.Configuration;

namespace Moe.Modules.FasPayment.Infrastructure.Documents;

public interface IFasDocumentStorage
{
    Task<string> UploadAsync(long applicationId, string fileName, Stream content, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
}

public interface IFasDocumentScanner { bool RequiresScan { get; } }
internal sealed class ConfiguredFasDocumentScanner(IConfiguration configuration) : IFasDocumentScanner
{ public bool RequiresScan => configuration.GetValue("FasDocuments:RequireMalwareScan", false); }

internal sealed class PrivateFileFasDocumentStorage : IFasDocumentStorage
{
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "App_Data", "fas-documents");
    public async Task<string> UploadAsync(long applicationId, string fileName, Stream content, CancellationToken ct)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant(); var key = $"{applicationId}/{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var output = File.Create(path); await content.CopyToAsync(output, ct); return key;
    }
    public Task DeleteAsync(string key, CancellationToken ct) { var p = Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)); if (File.Exists(p)) File.Delete(p); return Task.CompletedTask; }
    public Task<Stream> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult<Stream>(File.OpenRead(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar))));
}

internal sealed class AzureBlobFasDocumentStorage : IFasDocumentStorage
{
    private readonly BlobContainerClient container;
    private readonly PrivateFileFasDocumentStorage localFallback = new();
    public AzureBlobFasDocumentStorage(IConfiguration configuration)
    {
        var connection = configuration["FasDocuments:AzureBlobConnectionString"] ?? configuration["AzureBlob:ConnectionString"] ?? throw new InvalidOperationException("AzureBlob:ConnectionString is required.");
        var name = configuration["FasDocuments:ContainerName"] ?? configuration["AzureBlob:ContainerName"] ?? throw new InvalidOperationException("AzureBlob:ContainerName is required.");
        container = new BlobContainerClient(connection, name);
        container.CreateIfNotExists(PublicAccessType.None);
    }
    public async Task<string> UploadAsync(long applicationId, string fileName, Stream content, CancellationToken ct)
    { var key = $"fas-applications/{applicationId}/{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}"; await container.GetBlobClient(key).UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = Mime(fileName) } }, ct); return key; }
    public async Task DeleteAsync(string key, CancellationToken ct) { await container.GetBlobClient(key).DeleteIfExistsAsync(cancellationToken: ct); await localFallback.DeleteAsync(key, ct); }
    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        try
        {
            return (await container.GetBlobClient(key).DownloadStreamingAsync(cancellationToken: ct)).Value.Content;
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobNotFound.ToString())
        {
            return await localFallback.OpenReadAsync(key, ct);
        }
    }
    private static string Mime(string name) => Path.GetExtension(name).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".png" => "image/png", _ => "image/jpeg" };
}
