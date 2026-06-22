namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasSchemeCourse
{
    private FasSchemeCourse() { }
    public long FasSchemeId { get; private set; }
    public long CourseId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public static FasSchemeCourse Create(long schemeId, long courseId, DateTime utcNow)
    {
        if (schemeId <= 0) throw new ArgumentOutOfRangeException(nameof(schemeId));
        if (courseId <= 0) throw new ArgumentOutOfRangeException(nameof(courseId));
        return new FasSchemeCourse { FasSchemeId = schemeId, CourseId = courseId, CreatedAtUtc = utcNow };
    }
}
