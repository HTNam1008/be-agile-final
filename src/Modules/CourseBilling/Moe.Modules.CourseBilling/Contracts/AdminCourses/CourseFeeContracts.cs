namespace Moe.Modules.CourseBilling.Contracts.AdminCourses;

public sealed record CreateCourseFeeRequest(long FeeComponentId, decimal FeeValue, int SequenceNumber);

public sealed record UpdateCourseFeeRequest(decimal FeeValue, int SequenceNumber);

public sealed record CourseFeeDto(
    long CourseFeeId,
    long CourseId,
    long FeeComponentId,
    string ComponentCode,
    string ComponentName,
    string ComponentTypeCode,
    string CalculationTypeCode,
    decimal FeeValue,
    int SequenceNumber,
    bool IsSystemManaged,
    bool IsActive);
