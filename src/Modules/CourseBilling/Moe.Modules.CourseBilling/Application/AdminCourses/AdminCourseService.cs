using Microsoft.AspNetCore.Http;
using Moe.Application.Abstractions.Clock;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.IGateway.Storage;
using Moe.Modules.CourseBilling.Infrastructure.Security;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminCourses;

internal sealed class AdminCourseService(
    IAdminCourseRepository courses,
    ICourseMaterialStorageService storage,
    ICurrentAdminContext currentAdmin,
    IClock clock) : IAdminCourseService
{
    public async Task<Result<PageResponse<CourseSummaryDto>>> ListCoursesAsync(CourseQueryRequest request, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<PageResponse<CourseSummaryDto>>.Failure(admin.Error);

        return Result<PageResponse<CourseSummaryDto>>.Success(await courses.ListCoursesAsync(request, cancellationToken));
    }

    public async Task<Result<CourseDetailDto>> GetCourseAsync(long courseId, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<CourseDetailDto>.Failure(admin.Error);

        CourseAggregate? aggregate = await courses.GetCourseAggregateAsync(courseId, cancellationToken);
        return aggregate is null
            ? Result<CourseDetailDto>.Failure(CourseErrors.CourseNotFound)
            : Result<CourseDetailDto>.Success(ToDetail(aggregate));
    }

    public async Task<Result<CoursePreviewDto>> PreviewCourseAsync(long courseId, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<CoursePreviewDto>.Failure(admin.Error);

        CourseAggregate? aggregate = await courses.GetCourseAggregateAsync(courseId, cancellationToken);
        if (aggregate is null) return Result<CoursePreviewDto>.Failure(CourseErrors.CourseNotFound);

        CourseDetailDto detail = ToDetail(aggregate);
        CourseFeeDto[] activeFees = detail.Fees.Where(x => x.IsActive).ToArray();

        return Result<CoursePreviewDto>.Success(new CoursePreviewDto(
            detail,
            activeFees.Sum(x => x.FeeValue),
            activeFees.Length));
    }

    public async Task<Result<CourseDetailDto>> CreateCourseAsync(CreateCourseRequest request, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<CourseDetailDto>.Failure(admin.Error);

        DateTime utcNow = UtcNow();
        Result validation = await ValidateCourseInputAsync(request.CourseCode, request.StartDate, request.EndDate, utcNow, request.EnrollmentCloseAt, null, cancellationToken);
        if (validation.IsFailure) return Result<CourseDetailDto>.Failure(validation.Error);

        Course course = new(
            request.CourseCode,
            request.CourseName,
            request.Description,
            request.StartDate,
            request.EndDate,
            request.EnrollmentCloseAt,
            utcNow);

        await courses.AddCourseAsync(course, cancellationToken);
        return await GetCourseAsync(course.Id, cancellationToken);
    }

    public async Task<Result<CourseDetailDto>> UpdateCourseAsync(long courseId, UpdateCourseRequest request, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<CourseDetailDto>.Failure(admin.Error);

        Course? course = await courses.FindCourseAsync(courseId, cancellationToken);
        if (course is null) return Result<CourseDetailDto>.Failure(CourseErrors.CourseNotFound);
        if (course.IsDisabled) return Result<CourseDetailDto>.Failure(CourseErrors.CourseDisabled);

        Result validation = await ValidateCourseInputAsync(request.CourseCode, request.StartDate, request.EndDate, course.EnrollmentOpenAtUtc, request.EnrollmentCloseAt, courseId, cancellationToken);
        if (validation.IsFailure) return Result<CourseDetailDto>.Failure(validation.Error);

        course.Update(
            request.CourseCode,
            request.CourseName,
            request.Description,
            request.StartDate,
            request.EndDate,
            request.EnrollmentCloseAt,
            UtcNow());

        await courses.SaveChangesAsync(cancellationToken);
        return await GetCourseAsync(courseId, cancellationToken);
    }

    public async Task<Result<CourseDetailDto>> PublishCourseAsync(long courseId, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<CourseDetailDto>.Failure(admin.Error);

        Course? course = await courses.FindCourseAsync(courseId, cancellationToken);
        if (course is null) return Result<CourseDetailDto>.Failure(CourseErrors.CourseNotFound);
        if (course.IsDisabled) return Result<CourseDetailDto>.Failure(CourseErrors.CourseDisabled);
        if (!course.IsDraft) return Result<CourseDetailDto>.Failure(CourseErrors.CourseNotDraft);

        CourseAggregate? aggregate = await courses.GetCourseAggregateAsync(courseId, cancellationToken);
        CoursePublishReadinessDto readiness = BuildReadiness(aggregate!);
        if (!readiness.CanPublish)
        {
            return Result<CourseDetailDto>.Failure(new Error("COURSE.PUBLISH_VALIDATION_FAILED", string.Join(" ", readiness.Errors)));
        }

        course.Publish(UtcNow());
        await courses.SaveChangesAsync(cancellationToken);
        return await GetCourseAsync(courseId, cancellationToken);
    }

    public async Task<Result<CourseDetailDto>> DisableCourseAsync(long courseId, DisableCourseRequest request, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<CourseDetailDto>.Failure(admin.Error);

        Course? course = await courses.FindCourseAsync(courseId, cancellationToken);
        if (course is null) return Result<CourseDetailDto>.Failure(CourseErrors.CourseNotFound);

        course.Disable(UtcNow());
        await courses.SaveChangesAsync(cancellationToken);
        return await GetCourseAsync(courseId, cancellationToken);
    }

    public async Task<Result<CourseDetailDto>> EnableCourseAsync(long courseId, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<CourseDetailDto>.Failure(admin.Error);

        Course? course = await courses.FindCourseAsync(courseId, cancellationToken);
        if (course is null) return Result<CourseDetailDto>.Failure(CourseErrors.CourseNotFound);

        course.Enable(UtcNow());
        await courses.SaveChangesAsync(cancellationToken);
        return await GetCourseAsync(courseId, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<CourseMaterialDto>>> ListMaterialsAsync(long courseId, CancellationToken cancellationToken)
    {
        Result<Course> course = await RequireCourseAsync(courseId, cancellationToken);
        if (course.IsFailure) return Result<IReadOnlyList<CourseMaterialDto>>.Failure(course.Error);

        IReadOnlyList<CourseMaterial> materials = await courses.ListMaterialsAsync(courseId, cancellationToken);
        return Result<IReadOnlyList<CourseMaterialDto>>.Success(materials.Select(ToMaterialDto).ToArray());
    }

    public async Task<Result<CourseMaterialDto>> AddMaterialAsync(long courseId, CreateCourseMaterialRequest request, CancellationToken cancellationToken)
    {
        Result<Course> courseResult = await RequireMutableCourseAsync(courseId, cancellationToken);
        if (courseResult.IsFailure) return Result<CourseMaterialDto>.Failure(courseResult.Error);
        if (!IsValidMaterialType(request.MaterialTypeCode)) return Result<CourseMaterialDto>.Failure(CourseErrors.InvalidMaterialType);
        if (request.File is null || request.File.Length <= 0) return Result<CourseMaterialDto>.Failure(CourseErrors.InvalidFile);

        StoredCourseMaterialFile stored = await StoreFileAsync(courseId, request.File, cancellationToken);
        CourseMaterial material = new(
            courseId,
            request.MaterialTitle,
            request.MaterialDescription,
            request.MaterialTypeCode,
            stored.FileName,
            stored.OriginalFileName,
            stored.FileExtension,
            stored.ContentType,
            stored.FileSizeBytes,
            stored.StorageProviderCode,
            stored.StoragePath,
            stored.PublicUrl,
            request.DisplayOrder,
            request.IsRequired,
            UtcNow());

        await courses.AddMaterialAsync(material, cancellationToken);
        return Result<CourseMaterialDto>.Success(ToMaterialDto(material));
    }

    public async Task<Result<CourseMaterialDto>> UpdateMaterialAsync(long courseId, long courseMaterialId, UpdateCourseMaterialRequest request, CancellationToken cancellationToken)
    {
        Result<Course> courseResult = await RequireMutableCourseAsync(courseId, cancellationToken);
        if (courseResult.IsFailure) return Result<CourseMaterialDto>.Failure(courseResult.Error);
        if (!IsValidMaterialType(request.MaterialTypeCode)) return Result<CourseMaterialDto>.Failure(CourseErrors.InvalidMaterialType);

        CourseMaterial? material = await courses.FindMaterialAsync(courseId, courseMaterialId, cancellationToken);
        if (material is null || !material.IsActive) return Result<CourseMaterialDto>.Failure(CourseErrors.MaterialNotFound);

        material.UpdateMetadata(request.MaterialTitle, request.MaterialDescription, request.MaterialTypeCode, request.DisplayOrder, request.IsRequired, UtcNow());
        await courses.SaveChangesAsync(cancellationToken);
        return Result<CourseMaterialDto>.Success(ToMaterialDto(material));
    }

    public async Task<Result<CourseMaterialDto>> ReplaceMaterialFileAsync(long courseId, long courseMaterialId, ReplaceCourseMaterialFileRequest request, CancellationToken cancellationToken)
    {
        Result<Course> courseResult = await RequireMutableCourseAsync(courseId, cancellationToken);
        if (courseResult.IsFailure) return Result<CourseMaterialDto>.Failure(courseResult.Error);
        if (request.File is null || request.File.Length <= 0) return Result<CourseMaterialDto>.Failure(CourseErrors.InvalidFile);

        CourseMaterial? material = await courses.FindMaterialAsync(courseId, courseMaterialId, cancellationToken);
        if (material is null || !material.IsActive) return Result<CourseMaterialDto>.Failure(CourseErrors.MaterialNotFound);

        StoredCourseMaterialFile stored = await StoreFileAsync(courseId, request.File, cancellationToken);
        material.ReplaceFile(
            stored.FileName,
            stored.OriginalFileName,
            stored.FileExtension,
            stored.ContentType,
            stored.FileSizeBytes,
            stored.StorageProviderCode,
            stored.StoragePath,
            stored.PublicUrl,
            UtcNow());

        await courses.SaveChangesAsync(cancellationToken);
        return Result<CourseMaterialDto>.Success(ToMaterialDto(material));
    }

    public async Task<Result<CourseMaterialDto>> DeleteMaterialAsync(long courseId, long courseMaterialId, CancellationToken cancellationToken)
    {
        Result<Course> courseResult = await RequireMutableCourseAsync(courseId, cancellationToken);
        if (courseResult.IsFailure) return Result<CourseMaterialDto>.Failure(courseResult.Error);

        CourseMaterial? material = await courses.FindMaterialAsync(courseId, courseMaterialId, cancellationToken);
        if (material is null || !material.IsActive) return Result<CourseMaterialDto>.Failure(CourseErrors.MaterialNotFound);

        material.SoftDelete(UtcNow());
        await courses.SaveChangesAsync(cancellationToken);
        return Result<CourseMaterialDto>.Success(ToMaterialDto(material));
    }

    public async Task<Result<IReadOnlyList<CourseFeeDto>>> ListFeesAsync(long courseId, CancellationToken cancellationToken)
    {
        Result<Course> course = await RequireCourseAsync(courseId, cancellationToken);
        if (course.IsFailure) return Result<IReadOnlyList<CourseFeeDto>>.Failure(course.Error);

        IReadOnlyList<CourseFeeDetail> fees = await courses.ListFeesAsync(courseId, cancellationToken);
        return Result<IReadOnlyList<CourseFeeDto>>.Success(fees.Select(ToFeeDto).ToArray());
    }

    public async Task<Result<CourseFeeDto>> AddFeeAsync(long courseId, CreateCourseFeeRequest request, CancellationToken cancellationToken)
    {
        Result<Course> course = await RequireMutableCourseAsync(courseId, cancellationToken);
        if (course.IsFailure) return Result<CourseFeeDto>.Failure(course.Error);

        FeeComponent? component = await courses.FindActiveFeeComponentAsync(request.FeeComponentId, cancellationToken);
        if (component is null) return Result<CourseFeeDto>.Failure(CourseErrors.FeeComponentNotFound);
        if (await courses.FindCourseFeeByComponentAsync(courseId, request.FeeComponentId, cancellationToken) is not null)
        {
            return Result<CourseFeeDto>.Failure(CourseErrors.DuplicateCourseFee);
        }

        CourseFee fee = new(courseId, request.FeeComponentId, request.FeeValue, request.SequenceNumber);
        await courses.AddFeeAsync(fee, cancellationToken);
        return Result<CourseFeeDto>.Success(ToFeeDto(new CourseFeeDetail(fee, component)));
    }

    public async Task<Result<CourseFeeDto>> UpdateFeeAsync(long courseId, long courseFeeId, UpdateCourseFeeRequest request, CancellationToken cancellationToken)
    {
        Result<Course> course = await RequireMutableCourseAsync(courseId, cancellationToken);
        if (course.IsFailure) return Result<CourseFeeDto>.Failure(course.Error);

        CourseFee? fee = await courses.FindCourseFeeAsync(courseId, courseFeeId, cancellationToken);
        if (fee is null) return Result<CourseFeeDto>.Failure(CourseErrors.CourseFeeNotFound);
        FeeComponent? component = await courses.FindActiveFeeComponentAsync(fee.FeeComponentId, cancellationToken);
        if (component is null) return Result<CourseFeeDto>.Failure(CourseErrors.FeeComponentNotFound);

        fee.Update(request.FeeValue, request.SequenceNumber);
        await courses.SaveChangesAsync(cancellationToken);
        return Result<CourseFeeDto>.Success(ToFeeDto(new CourseFeeDetail(fee, component)));
    }

    public async Task<Result<CourseFeeDto>> DeleteFeeAsync(long courseId, long courseFeeId, CancellationToken cancellationToken)
    {
        Result<Course> course = await RequireMutableCourseAsync(courseId, cancellationToken);
        if (course.IsFailure) return Result<CourseFeeDto>.Failure(course.Error);

        CourseFee? fee = await courses.FindCourseFeeAsync(courseId, courseFeeId, cancellationToken);
        if (fee is null) return Result<CourseFeeDto>.Failure(CourseErrors.CourseFeeNotFound);
        FeeComponent? component = await courses.FindActiveFeeComponentAsync(fee.FeeComponentId, cancellationToken);
        if (component is null) return Result<CourseFeeDto>.Failure(CourseErrors.FeeComponentNotFound);

        fee.Deactivate();
        await courses.SaveChangesAsync(cancellationToken);
        return Result<CourseFeeDto>.Success(ToFeeDto(new CourseFeeDetail(fee, component)));
    }

    public async Task<Result<AssignStudentsToCourseResultDto>> AssignStudentsAsync(long courseId, AssignStudentsToCourseRequest request, CancellationToken cancellationToken)
    {
        Result<Course> courseResult = await RequireCourseAsync(courseId, cancellationToken);
        if (courseResult.IsFailure) return Result<AssignStudentsToCourseResultDto>.Failure(courseResult.Error);

        Course course = courseResult.Value;
        if (!course.IsPublished) return Result<AssignStudentsToCourseResultDto>.Failure(CourseErrors.CourseNotPublished);
        if (!IsEnrollmentWindowOpen(course, UtcNow())) return Result<AssignStudentsToCourseResultDto>.Failure(CourseErrors.EnrollmentWindowClosed);
        List<AssignStudentResultDto> results = [];
        foreach (long personId in request.PersonIds.Distinct())
        {
            if (await courses.HasActiveEnrollmentAsync(courseId, personId, cancellationToken))
            {
                results.Add(new AssignStudentResultDto(personId, false, null, "Student already enrolled in this course."));
                continue;
            }

            CourseEnrollment enrollment = new(personId, courseId, UtcNow());
            await courses.AddEnrollmentAsync(enrollment, cancellationToken);
            results.Add(new AssignStudentResultDto(personId, true, enrollment.Id, "Assigned successfully."));
        }

        // Billing generation is handled by the Student Enrollment/Billing module.
        return Result<AssignStudentsToCourseResultDto>.Success(new AssignStudentsToCourseResultDto(
            courseId,
            request.PersonIds.Count,
            results.Count(x => x.Success),
            results.Count(x => !x.Success),
            results));
    }

    public async Task<Result<IReadOnlyList<AdminCourseEnrollmentDto>>> ListEnrollmentsAsync(long courseId, CancellationToken cancellationToken)
    {
        Result<Course> course = await RequireCourseAsync(courseId, cancellationToken);
        if (course.IsFailure) return Result<IReadOnlyList<AdminCourseEnrollmentDto>>.Failure(course.Error);

        return Result<IReadOnlyList<AdminCourseEnrollmentDto>>.Success(await courses.ListEnrollmentsAsync(courseId, cancellationToken));
    }

    public async Task<Result<AdminCourseEnrollmentDto>> RemoveEnrollmentAsync(long courseId, long courseEnrollmentId, CancellationToken cancellationToken)
    {
        Result<Course> course = await RequireMutableCourseAsync(courseId, cancellationToken);
        if (course.IsFailure) return Result<AdminCourseEnrollmentDto>.Failure(course.Error);

        CourseEnrollment? enrollment = await courses.FindEnrollmentAsync(courseEnrollmentId, cancellationToken);
        if (enrollment is null || enrollment.CourseId != courseId)
        {
            return Result<AdminCourseEnrollmentDto>.Failure(CourseErrors.EnrollmentNotFound);
        }

        AdminCourseEnrollmentDto removed = ToEnrollmentDto(enrollment);
        courses.RemoveEnrollment(enrollment);
        await courses.SaveChangesAsync(cancellationToken);
        return Result<AdminCourseEnrollmentDto>.Success(removed);
    }

    private async Task<Result<Course>> RequireCourseAsync(long courseId, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<Course>.Failure(admin.Error);

        Course? course = await courses.FindCourseAsync(courseId, cancellationToken);
        return course is null
            ? Result<Course>.Failure(CourseErrors.CourseNotFound)
            : Result<Course>.Success(course);
    }

    private async Task<Result<Course>> RequireMutableCourseAsync(long courseId, CancellationToken cancellationToken)
    {
        Result<Course> course = await RequireCourseAsync(courseId, cancellationToken);
        if (course.IsFailure) return course;
        return course.Value.IsDisabled
            ? Result<Course>.Failure(CourseErrors.CourseDisabled)
            : course;
    }

    private Result RequireAdmin()
        => currentAdmin.IsAdmin ? Result.Success() : Result.Failure(CourseErrors.AdminRequired);

    private async Task<Result> ValidateCourseInputAsync(
        string courseCode,
        DateOnly startDate,
        DateOnly endDate,
        DateTime enrollmentOpenAt,
        DateTime enrollmentCloseAt,
        long? excludeCourseId,
        CancellationToken cancellationToken)
    {
        if (startDate > endDate)
        {
            return Result.Failure(CourseErrors.InvalidDateRange);
        }

        if (enrollmentOpenAt > enrollmentCloseAt)
        {
            return Result.Failure(CourseErrors.InvalidEnrollmentWindow);
        }

        if (await courses.CourseCodeExistsAsync(courseCode, excludeCourseId, cancellationToken))
        {
            return Result.Failure(CourseErrors.DuplicateCourseCode);
        }

        return Result.Success();
    }

    private static bool IsEnrollmentWindowOpen(Course course, DateTime utcNow)
    {
        if (utcNow < course.EnrollmentOpenAtUtc) return false;
        if (utcNow > course.EnrollmentCloseAtUtc) return false;
        return true;
    }

    private async Task<StoredCourseMaterialFile> StoreFileAsync(long courseId, IFormFile file, CancellationToken cancellationToken)
    {
        await using Stream stream = file.OpenReadStream();
        return await storage.SaveAsync(courseId, file.FileName, file.ContentType, stream, cancellationToken);
    }

    private static bool IsValidMaterialType(string materialTypeCode)
        => CourseMaterialTypeCodes.All.Contains(materialTypeCode, StringComparer.OrdinalIgnoreCase);

    private CourseDetailDto ToDetail(CourseAggregate aggregate)
    {
        IReadOnlyList<CourseMaterialDto> materials = aggregate.Materials.Select(ToMaterialDto).ToArray();
        IReadOnlyList<CourseFeeDto> fees = aggregate.Fees.Select(ToFeeDto).ToArray();

        return new CourseDetailDto(
            aggregate.Course.Id,
            aggregate.Course.CourseCode,
            aggregate.Course.CourseName,
            aggregate.Course.Description,
            aggregate.Course.StartDate,
            aggregate.Course.EndDate,
            aggregate.Course.EnrollmentOpenAtUtc,
            aggregate.Course.EnrollmentCloseAtUtc,
            aggregate.Course.CourseStatusCode,
            aggregate.Course.UpdatedAtUtc,
            aggregate.Course.DisabledAtUtc,
            materials,
            fees,
            aggregate.EnrollmentSummary,
            BuildReadiness(aggregate));
    }

    private static CoursePublishReadinessDto BuildReadiness(CourseAggregate aggregate)
    {
        List<string> errors = [];
        List<string> warnings = [];
        Course course = aggregate.Course;

        if (string.IsNullOrWhiteSpace(course.CourseCode)) errors.Add("Course code is required.");
        if (string.IsNullOrWhiteSpace(course.CourseName)) errors.Add("Course name is required.");
        if (course.IsDisabled) errors.Add("Course must not be disabled.");
        if (course.StartDate > course.EndDate) errors.Add("Start date cannot be after end date.");
        if (course.EnrollmentOpenAtUtc > course.EnrollmentCloseAtUtc) errors.Add("Enrollment open date cannot be after enrollment close date.");
        if (!aggregate.Materials.Any(x => x.IsActive)) warnings.Add("No material uploaded.");
        if (!aggregate.Fees.Any(x => x.CourseFee.IsActive)) warnings.Add("No fee configured.");

        string step = course.IsDisabled
            ? "DISABLED"
            : course.IsPublished
                ? "PUBLISHED"
                : aggregate.Fees.Any(x => x.CourseFee.IsActive)
                    ? "READY_TO_PUBLISH"
                    : aggregate.Materials.Any(x => x.IsActive)
                        ? "FEES"
                        : "MATERIALS";

        return new CoursePublishReadinessDto(errors.Count == 0, errors, warnings, step);
    }

    private static CourseMaterialDto ToMaterialDto(CourseMaterial material)
        => new(
            material.Id,
            material.CourseId,
            material.MaterialTitle,
            material.MaterialDescription,
            material.MaterialTypeCode,
            material.FileName,
            material.OriginalFileName,
            material.FileExtension,
            material.ContentType,
            material.FileSizeBytes,
            material.StorageProviderCode,
            material.StoragePath,
            material.PublicUrl,
            material.DisplayOrder,
            material.IsRequired,
            material.IsActive,
            material.UploadedAtUtc,
            material.UpdatedAtUtc,
            material.DeletedAtUtc);

    private static CourseFeeDto ToFeeDto(CourseFeeDetail detail)
        => new(
            detail.CourseFee.Id,
            detail.CourseFee.CourseId,
            detail.CourseFee.FeeComponentId,
            detail.FeeComponent.ComponentCode,
            detail.FeeComponent.ComponentName,
            detail.FeeComponent.ComponentTypeCode,
            detail.FeeComponent.CalculationTypeCode,
            detail.CourseFee.FeeValue,
            detail.CourseFee.SequenceNumber,
            detail.CourseFee.IsActive);

    private static AdminCourseEnrollmentDto ToEnrollmentDto(CourseEnrollment enrollment)
        => new(
            enrollment.Id,
            enrollment.CourseId,
            enrollment.PersonId,
            null,
            enrollment.EnrollmentSourceCode,
            enrollment.EnrolledByLoginAccountId,
            enrollment.EnrolledAtUtc,
            enrollment.EnrollmentStatusCode);

    private DateTime UtcNow() => clock.UtcNow.UtcDateTime;
}
