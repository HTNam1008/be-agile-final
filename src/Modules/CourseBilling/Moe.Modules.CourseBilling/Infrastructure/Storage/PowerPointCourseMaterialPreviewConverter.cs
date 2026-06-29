using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Moe.Modules.CourseBilling.IGateway.Storage;

namespace Moe.Modules.CourseBilling.Infrastructure.Storage;

internal sealed class PowerPointCourseMaterialPreviewConverter : ICourseMaterialPreviewConverter
{
    private const int PpSaveAsPdf = 32;
    private const int MsoFalse = 0;

    public async Task<Stream?> TryConvertToPdfAsync(
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (!IsPowerPointFile(originalFileName, contentType) || !OperatingSystem.IsWindows())
            return null;

        string workDir = Path.Combine(Path.GetTempPath(), "moe-course-material-preview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        string extension = Path.GetExtension(originalFileName);
        string inputPath = Path.Combine(workDir, $"source{extension}");
        string outputPath = Path.Combine(workDir, "preview.pdf");

        await using (FileStream input = File.Create(inputPath))
        {
            await content.CopyToAsync(input, cancellationToken);
        }

        try
        {
            byte[] pdfBytes = await ConvertWithPowerPointAsync(inputPath, outputPath, cancellationToken);
            return new MemoryStream(pdfBytes);
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    [SupportedOSPlatform("windows")]
    private static Task<byte[]> ConvertWithPowerPointAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource<byte[]> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread thread = new(() =>
        {
            object? application = null;
            object? presentations = null;
            object? presentation = null;

            try
            {
                Type powerPointType = Type.GetTypeFromProgID("PowerPoint.Application")
                    ?? throw new InvalidOperationException("Microsoft PowerPoint is not installed.");

                application = Activator.CreateInstance(powerPointType)
                    ?? throw new InvalidOperationException("Microsoft PowerPoint could not be started.");

                presentations = application.GetType().InvokeMember(
                    "Presentations",
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    application,
                    null);

                presentation = presentations?.GetType().InvokeMember(
                    "Open",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    presentations,
                    new object[] { inputPath, MsoFalse, MsoFalse, MsoFalse });

                presentation?.GetType().InvokeMember(
                    "SaveAs",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    presentation,
                    new object[] { outputPath, PpSaveAsPdf, MsoFalse });

                completion.TrySetResult(File.ReadAllBytes(outputPath));
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
            finally
            {
                TryInvoke(presentation, "Close");
                TryInvoke(application, "Quit");
                ReleaseComObject(presentation);
                ReleaseComObject(presentations);
                ReleaseComObject(application);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return completion.Task;
    }

    private static bool IsPowerPointFile(string originalFileName, string contentType)
    {
        string extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (extension is ".ppt" or ".pptx" or ".pps" or ".ppsx")
            return true;

        string normalizedContentType = contentType.ToLowerInvariant();
        return normalizedContentType.Contains("powerpoint")
            || normalizedContentType.Contains("presentationml");
    }

    private static void TryInvoke(object? target, string memberName)
    {
        try
        {
            target?.GetType().InvokeMember(
                memberName,
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                target,
                null);
        }
        catch
        {
            // Best-effort COM cleanup only.
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ReleaseComObject(object? target)
    {
        try
        {
            if (target is not null && Marshal.IsComObject(target))
                Marshal.FinalReleaseComObject(target);
        }
        catch
        {
            // Best-effort COM cleanup only.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Temp cleanup should never fail the download response.
        }
    }
}
