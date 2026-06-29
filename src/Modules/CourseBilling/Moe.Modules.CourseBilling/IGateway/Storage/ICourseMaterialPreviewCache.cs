using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.IGateway.Storage;

internal interface ICourseMaterialPreviewCache
{
    Task<Stream?> GetPdfAsync(CourseMaterial material, CancellationToken cancellationToken);

    Task SetPdfAsync(CourseMaterial material, byte[] pdfBytes, CancellationToken cancellationToken);
}
