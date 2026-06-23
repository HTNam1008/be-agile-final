namespace Moe.Modules.CourseBilling.IGateway.Courses;

public interface ICourseReferenceDirectory
{
    Task<IReadOnlyList<long>> FindUnknownCourseIdsAsync(
        IReadOnlyCollection<long> courseIds,
        CancellationToken cancellationToken);
}
