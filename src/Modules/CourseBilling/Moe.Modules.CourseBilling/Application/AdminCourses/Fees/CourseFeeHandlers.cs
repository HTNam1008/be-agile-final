using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Fees;

internal sealed class ListCourseFeesQueryHandler(AdminCourseAccess access)
    : IQueryHandler<ListCourseFeesQuery, IReadOnlyList<CourseFeeDto>>
{
    public async Task<Result<IReadOnlyList<CourseFeeDto>>> Handle(
        ListCourseFeesQuery query,
        CancellationToken cancellationToken)
    {
        Result<Course> course = await access.RequireCourseAsync(query.CourseId, cancellationToken);
        if (course.IsFailure)
        {
            return Result<IReadOnlyList<CourseFeeDto>>.Failure(course.Error);
        }

        IReadOnlyList<CourseFeeDetail> fees = await access.Courses.ListFeesAsync(query.CourseId, cancellationToken);
        return Result<IReadOnlyList<CourseFeeDto>>.Success(fees.Select(CourseFeeMapper.ToFeeDto).ToArray());
    }
}

internal sealed class AddCourseFeeCommandHandler(AdminCourseAccess access)
    : ICommandHandler<AddCourseFeeCommand, CourseFeeDto>
{
    public async Task<Result<CourseFeeDto>> Handle(AddCourseFeeCommand command, CancellationToken cancellationToken)
    {
        CreateCourseFeeRequest request = command.Request;

        Result<Course> course = await access.RequireMutableCourseAsync(command.CourseId, cancellationToken);
        if (course.IsFailure)
        {
            return Result<CourseFeeDto>.Failure(course.Error);
        }

        FeeComponent? component = await access.Courses.FindActiveFeeComponentAsync(request.FeeComponentId, cancellationToken);
        if (component is null)
        {
            return Result<CourseFeeDto>.Failure(CourseErrors.FeeComponentNotFound);
        }

        if (await access.Courses.FindCourseFeeByComponentAsync(command.CourseId, request.FeeComponentId, cancellationToken) is not null)
        {
            return Result<CourseFeeDto>.Failure(CourseErrors.DuplicateCourseFee);
        }

        CourseFee fee = new(command.CourseId, request.FeeComponentId, request.FeeValue, request.SequenceNumber);
        await access.Courses.AddFeeAsync(fee, cancellationToken);
        return Result<CourseFeeDto>.Success(CourseFeeMapper.ToFeeDto(new CourseFeeDetail(fee, component)));
    }
}

internal sealed class UpdateCourseFeeCommandHandler(AdminCourseAccess access)
    : ICommandHandler<UpdateCourseFeeCommand, CourseFeeDto>
{
    public async Task<Result<CourseFeeDto>> Handle(UpdateCourseFeeCommand command, CancellationToken cancellationToken)
    {
        UpdateCourseFeeRequest request = command.Request;

        Result<Course> course = await access.RequireMutableCourseAsync(command.CourseId, cancellationToken);
        if (course.IsFailure)
        {
            return Result<CourseFeeDto>.Failure(course.Error);
        }

        CourseFee? fee = await access.Courses.FindCourseFeeAsync(
            command.CourseId,
            command.CourseFeeId,
            cancellationToken);
        if (fee is null)
        {
            return Result<CourseFeeDto>.Failure(CourseErrors.CourseFeeNotFound);
        }

        FeeComponent? component = await access.Courses.FindActiveFeeComponentAsync(fee.FeeComponentId, cancellationToken);
        if (component is null)
        {
            return Result<CourseFeeDto>.Failure(CourseErrors.FeeComponentNotFound);
        }

        fee.Update(request.FeeValue, request.SequenceNumber);
        await access.Courses.SaveChangesAsync(cancellationToken);
        return Result<CourseFeeDto>.Success(CourseFeeMapper.ToFeeDto(new CourseFeeDetail(fee, component)));
    }
}

internal sealed class DeleteCourseFeeCommandHandler(AdminCourseAccess access)
    : ICommandHandler<DeleteCourseFeeCommand, CourseFeeDto>
{
    public async Task<Result<CourseFeeDto>> Handle(DeleteCourseFeeCommand command, CancellationToken cancellationToken)
    {
        Result<Course> course = await access.RequireMutableCourseAsync(command.CourseId, cancellationToken);
        if (course.IsFailure)
        {
            return Result<CourseFeeDto>.Failure(course.Error);
        }

        CourseFee? fee = await access.Courses.FindCourseFeeAsync(
            command.CourseId,
            command.CourseFeeId,
            cancellationToken);
        if (fee is null)
        {
            return Result<CourseFeeDto>.Failure(CourseErrors.CourseFeeNotFound);
        }

        FeeComponent? component = await access.Courses.FindActiveFeeComponentAsync(fee.FeeComponentId, cancellationToken);
        if (component is null)
        {
            return Result<CourseFeeDto>.Failure(CourseErrors.FeeComponentNotFound);
        }

        fee.Deactivate();
        await access.Courses.SaveChangesAsync(cancellationToken);
        return Result<CourseFeeDto>.Success(CourseFeeMapper.ToFeeDto(new CourseFeeDetail(fee, component)));
    }
}
