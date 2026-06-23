namespace Moe.Modules.CourseBilling.Domain.Courses;

internal static class CourseMaterialTypeCodes
{
    public const string Syllabus = "SYLLABUS";
    public const string LessonNote = "LESSON_NOTE";
    public const string ReadingMaterial = "READING_MATERIAL";
    public const string Assignment = "ASSIGNMENT";
    public const string Guide = "GUIDE";
    public const string Other = "OTHER";

    public static readonly string[] All =
    [
        Syllabus,
        LessonNote,
        ReadingMaterial,
        Assignment,
        Guide,
        Other
    ];
}
