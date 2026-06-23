using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal static class CourseErrors
{
    public static readonly Error AdminRequired = new("COURSE.ADMIN_REQUIRED", "Only ADMIN can access this course management workflow.");
    public static readonly Error CourseNotFound = new("COURSE.NOT_FOUND", "Course was not found.");
    public static readonly Error CourseDisabled = new("COURSE.DISABLED", "Disabled courses cannot be modified.");
    public static readonly Error CourseNotDraft = new("COURSE.NOT_DRAFT", "Only draft courses can be published.");
    public static readonly Error DraftRequiredForRemoval = new("COURSE.DRAFT_REQUIRED_FOR_REMOVAL", "Only draft courses can be removed.");
    public static readonly Error CourseNotPublished = new("COURSE.NOT_PUBLISHED", "Course must be published before students can be assigned.");
    public static readonly Error DuplicateCourseCode = new("COURSE.DUPLICATE_CODE", "Course code already exists for this academic year.");
    public static readonly Error InvalidDateRange = new("COURSE.INVALID_DATE_RANGE", "Start date cannot be after end date.");
    public static readonly Error CourseDateInPast = new("COURSE.DATE_IN_PAST", "Course start and end dates cannot be before today.");
    public static readonly Error InvalidEnrollmentWindow = new("COURSE.INVALID_ENROLLMENT_WINDOW", "Enrollment open date cannot be after enrollment close date.");
    public static readonly Error EnrollmentDateInPast = new("COURSE.ENROLLMENT_DATE_IN_PAST", "Enrollment start and end times cannot be in the past.");
    public static readonly Error EnrollmentMustCloseBeforeCourseStarts = new("COURSE.ENROLLMENT_MUST_CLOSE_BEFORE_START", "Enrollment must close before the course start date.");
    public static readonly Error EnrollmentWindowClosed = new("COURSE.ENROLLMENT_WINDOW_CLOSED", "Course enrollment is not open.");
    public static readonly Error InvalidRefundPercentage = new("COURSE.INVALID_REFUND_PERCENTAGE", "Refund percentages must be between 0 and 100.");
    public static readonly Error FeeComponentNotFound = new("COURSE.FEE_COMPONENT_NOT_FOUND", "Fee component was not found or is inactive.");
    public static readonly Error DuplicateFeeComponentCode = new("COURSE.FEE_COMPONENT_DUPLICATE_CODE", "Fee component code already exists.");
    public static readonly Error InvalidFeeComponentType = new("COURSE.INVALID_FEE_COMPONENT_TYPE", "Fee component type must be TUITION, MATERIAL or TAX.");
    public static readonly Error InvalidCalculationType = new("COURSE.INVALID_CALCULATION_TYPE", "Calculation type must be FIXED or PERCENTAGE.");
    public static readonly Error DuplicateCourseFee = new("COURSE.DUPLICATE_FEE_COMPONENT", "This fee component is already configured for the course.");
    public static readonly Error CourseFeeNotFound = new("COURSE.FEE_NOT_FOUND", "Course fee was not found.");
    public static readonly Error MaterialNotFound = new("COURSE.MATERIAL_NOT_FOUND", "Course material was not found.");
    public static readonly Error InvalidMaterialType = new("COURSE.INVALID_MATERIAL_TYPE", "Material type code is not supported.");
    public static readonly Error InvalidFile = new("COURSE.INVALID_FILE", "A non-empty material file is required.");
    public static readonly Error EnrollmentNotFound = new("COURSE.ENROLLMENT_NOT_FOUND", "Course enrollment was not found.");
}
