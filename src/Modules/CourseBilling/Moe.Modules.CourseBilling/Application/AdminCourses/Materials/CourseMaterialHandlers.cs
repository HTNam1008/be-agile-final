using Microsoft.AspNetCore.Http;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Storage;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Materials;

internal sealed class ListCourseMaterialsQueryHandler(AdminCourseAccess access)
    : IQueryHandler<ListCourseMaterialsQuery, IReadOnlyList<CourseMaterialDto>>
{
    public async Task<Result<IReadOnlyList<CourseMaterialDto>>> Handle(
        ListCourseMaterialsQuery query,
        CancellationToken cancellationToken)
    {
        Result<Course> course = await access.RequireCourseAsync(query.CourseId, cancellationToken);
        if (course.IsFailure)
        {
            return Result<IReadOnlyList<CourseMaterialDto>>.Failure(course.Error);
        }

        IReadOnlyList<CourseMaterial> materials = await access.Courses.ListMaterialsAsync(query.CourseId, cancellationToken);
        return Result<IReadOnlyList<CourseMaterialDto>>.Success(
            materials.Select(CourseMaterialMapper.ToMaterialDto).ToArray());
    }
}

internal sealed class AddCourseMaterialCommandHandler(
    AdminCourseAccess access,
    ICourseMaterialStorageService storage)
    : ICommandHandler<AddCourseMaterialCommand, CourseMaterialDto>
{
    public async Task<Result<CourseMaterialDto>> Handle(AddCourseMaterialCommand command, CancellationToken cancellationToken)
    {
        CreateCourseMaterialRequest request = command.Request;

        Result<Course> course = await access.RequireMutableCourseAsync(command.CourseId, cancellationToken);
        if (course.IsFailure)
        {
            return Result<CourseMaterialDto>.Failure(course.Error);
        }

        if (!IsValidMaterialType(request.MaterialTypeCode))
        {
            return Result<CourseMaterialDto>.Failure(CourseErrors.InvalidMaterialType);
        }

        if (request.File is null || request.File.Length <= 0)
        {
            return Result<CourseMaterialDto>.Failure(CourseErrors.InvalidFile);
        }

        if (CourseMaterialFileHelper.ExceedsMaxFileSize(request.File))
        {
            return Result<CourseMaterialDto>.Failure(CourseErrors.MaterialFileTooLarge);
        }

        if (!CourseMaterialFileHelper.IsSupported(request.File))
        {
            return Result<CourseMaterialDto>.Failure(CourseErrors.UnsupportedMaterialFileType);
        }

        StoredCourseMaterialFile stored = await CourseMaterialFileHelper.StoreFileAsync(storage, command.CourseId, request.File, cancellationToken);
        CourseMaterial material = new(
            command.CourseId,
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
            access.UtcNow());

        await access.Courses.AddMaterialAsync(material, cancellationToken);
        return Result<CourseMaterialDto>.Success(CourseMaterialMapper.ToMaterialDto(material));
    }

    private static bool IsValidMaterialType(string materialTypeCode)
        => CourseMaterialTypeCodes.All.Contains(materialTypeCode, StringComparer.OrdinalIgnoreCase);
}

internal sealed class UpdateCourseMaterialCommandHandler(AdminCourseAccess access)
    : ICommandHandler<UpdateCourseMaterialCommand, CourseMaterialDto>
{
    public async Task<Result<CourseMaterialDto>> Handle(UpdateCourseMaterialCommand command, CancellationToken cancellationToken)
    {
        UpdateCourseMaterialRequest request = command.Request;

        Result<Course> course = await access.RequireMutableCourseAsync(command.CourseId, cancellationToken);
        if (course.IsFailure)
        {
            return Result<CourseMaterialDto>.Failure(course.Error);
        }

        if (!CourseMaterialTypeCodes.All.Contains(request.MaterialTypeCode, StringComparer.OrdinalIgnoreCase))
        {
            return Result<CourseMaterialDto>.Failure(CourseErrors.InvalidMaterialType);
        }

        CourseMaterial? material = await access.Courses.FindMaterialAsync(
            command.CourseId,
            command.CourseMaterialId,
            cancellationToken);
        if (material is null || !material.IsActive)
        {
            return Result<CourseMaterialDto>.Failure(CourseErrors.MaterialNotFound);
        }

        material.UpdateMetadata(
            request.MaterialTitle,
            request.MaterialDescription,
            request.MaterialTypeCode,
            request.DisplayOrder,
            request.IsRequired,
            access.UtcNow());
        await access.Courses.SaveMaterialAsync(material, cancellationToken);

        return Result<CourseMaterialDto>.Success(CourseMaterialMapper.ToMaterialDto(material));
    }
}

internal sealed class ReplaceCourseMaterialFileCommandHandler(
    AdminCourseAccess access,
    ICourseMaterialStorageService storage)
    : ICommandHandler<ReplaceCourseMaterialFileCommand, CourseMaterialDto>
{
    public async Task<Result<CourseMaterialDto>> Handle(
        ReplaceCourseMaterialFileCommand command,
        CancellationToken cancellationToken)
    {
        ReplaceCourseMaterialFileRequest request = command.Request;

        Result<Course> course = await access.RequireMutableCourseAsync(command.CourseId, cancellationToken);
        if (course.IsFailure)
        {
            return Result<CourseMaterialDto>.Failure(course.Error);
        }

        if (request.File is null || request.File.Length <= 0)
        {
            return Result<CourseMaterialDto>.Failure(CourseErrors.InvalidFile);
        }

        if (CourseMaterialFileHelper.ExceedsMaxFileSize(request.File))
        {
            return Result<CourseMaterialDto>.Failure(CourseErrors.MaterialFileTooLarge);
        }

        if (!CourseMaterialFileHelper.IsSupported(request.File))
        {
            return Result<CourseMaterialDto>.Failure(CourseErrors.UnsupportedMaterialFileType);
        }

        CourseMaterial? material = await access.Courses.FindMaterialAsync(
            command.CourseId,
            command.CourseMaterialId,
            cancellationToken);
        if (material is null || !material.IsActive)
        {
            return Result<CourseMaterialDto>.Failure(CourseErrors.MaterialNotFound);
        }

        StoredCourseMaterialFile stored = await CourseMaterialFileHelper.StoreFileAsync(storage, command.CourseId, request.File, cancellationToken);
        material.ReplaceFile(
            stored.FileName,
            stored.OriginalFileName,
            stored.FileExtension,
            stored.ContentType,
            stored.FileSizeBytes,
            stored.StorageProviderCode,
            stored.StoragePath,
            stored.PublicUrl,
            access.UtcNow());

        await access.Courses.SaveMaterialAsync(material, cancellationToken);
        return Result<CourseMaterialDto>.Success(CourseMaterialMapper.ToMaterialDto(material));
    }
}

internal sealed class DeleteCourseMaterialCommandHandler(AdminCourseAccess access)
    : ICommandHandler<DeleteCourseMaterialCommand, CourseMaterialDto>
{
    public async Task<Result<CourseMaterialDto>> Handle(DeleteCourseMaterialCommand command, CancellationToken cancellationToken)
    {
        Result<Course> course = await access.RequireMutableCourseAsync(command.CourseId, cancellationToken);
        if (course.IsFailure)
        {
            return Result<CourseMaterialDto>.Failure(course.Error);
        }

        CourseMaterial? material = await access.Courses.FindMaterialAsync(
            command.CourseId,
            command.CourseMaterialId,
            cancellationToken);
        if (material is null || !material.IsActive)
        {
            return Result<CourseMaterialDto>.Failure(CourseErrors.MaterialNotFound);
        }

        material.SoftDelete(access.UtcNow());
        await access.Courses.SaveMaterialAsync(material, cancellationToken);
        return Result<CourseMaterialDto>.Success(CourseMaterialMapper.ToMaterialDto(material));
    }
}
