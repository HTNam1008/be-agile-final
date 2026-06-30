namespace Moe.Modules.CourseBilling.IGateway.Storage;

internal interface ICourseMaterialPreviewConverter
{
    Task<Stream?> TryConvertToPdfAsync(
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);
}
