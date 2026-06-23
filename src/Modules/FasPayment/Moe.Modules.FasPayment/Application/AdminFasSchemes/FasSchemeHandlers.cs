using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.Modules.CourseBilling.IGateway.Courses;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.AdminFasSchemes;

internal static class FasSchemeErrors
{
    public static readonly Error NotFound = new("FAS.SCHEME_NOT_FOUND", "The FAS scheme was not found.");
    public static readonly Error Unauthenticated = new("FAS.ACTOR_REQUIRED", "An authenticated administrator is required.");
    public static readonly Error DuplicateSchemeCode = new("FAS.DUPLICATE_SCHEME_CODE", "A scheme with the same scheme code already exists.");
    public static readonly Error DuplicateGrantCode = new("FAS.DUPLICATE_GRANT_CODE", "A scheme with the same grant code already exists.");
    public static Error UnknownCourses(IEnumerable<long> ids) => new("FAS.UNKNOWN_COURSES", $"Unknown course IDs: {string.Join(", ", ids)}.");
}

internal sealed class CreateFasSchemeHandler(
    IFasSchemeRepository repository,
    ICourseReferenceDirectory courses,
    ICurrentUser currentUser,
    IClock clock)
    : ICommandHandler<CreateFasSchemeCommand, CreateFasSchemeResponse>
{
    public async Task<Result<CreateFasSchemeResponse>> Handle(CreateFasSchemeCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is not long actorId) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.Unauthenticated);
        if (await repository.SchemeCodeExistsAsync(command.Request.SchemeCode, cancellationToken)) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.DuplicateSchemeCode);
        if (await repository.GrantCodeExistsAsync(command.Request.GrantCode, cancellationToken)) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.DuplicateGrantCode);
        IReadOnlyList<long> unknown = await courses.FindUnknownCourseIdsAsync(command.Request.CourseIds.Distinct().ToArray(), cancellationToken);
        if (unknown.Count > 0) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.UnknownCourses(unknown));
        try
        {
            return Result<CreateFasSchemeResponse>.Success(await repository.CreateAsync(command.Request, actorId, clock.UtcNow.UtcDateTime, cancellationToken));
        }
        catch (FasSchemeWriteConflictException exception)
        {
            Error error = exception.Field == FasSchemeUniqueField.GrantCode
                ? FasSchemeErrors.DuplicateGrantCode
                : FasSchemeErrors.DuplicateSchemeCode;
            return Result<CreateFasSchemeResponse>.Failure(error);
        }
    }
}

internal sealed class SaveFasSchemeDraftHandler(
    IFasSchemeRepository repository, ICourseReferenceDirectory courses, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<SaveFasSchemeDraftCommand, CreateFasSchemeResponse>
{
    public async Task<Result<CreateFasSchemeResponse>> Handle(SaveFasSchemeDraftCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is not long actorId) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.Unauthenticated);
        bool duplicateScheme = command.SchemeId.HasValue
            ? await repository.SchemeCodeExistsExcludingAsync(command.Request.SchemeCode, command.SchemeId.Value, cancellationToken)
            : await repository.SchemeCodeExistsAsync(command.Request.SchemeCode, cancellationToken);
        bool duplicateGrant = command.SchemeId.HasValue
            ? await repository.GrantCodeExistsExcludingAsync(command.Request.GrantCode, command.SchemeId.Value, cancellationToken)
            : await repository.GrantCodeExistsAsync(command.Request.GrantCode, cancellationToken);
        if (duplicateScheme) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.DuplicateSchemeCode);
        if (duplicateGrant) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.DuplicateGrantCode);
        IReadOnlyList<long> unknown = await courses.FindUnknownCourseIdsAsync(command.Request.CourseIds.Distinct().ToArray(), cancellationToken);
        if (unknown.Count > 0) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.UnknownCourses(unknown));
        try
        {
            return Result<CreateFasSchemeResponse>.Success(await repository.SaveDraftAsync(command.SchemeId, command.Request, actorId, clock.UtcNow.UtcDateTime, cancellationToken));
        }
        catch (InvalidOperationException) when (command.SchemeId.HasValue)
        {
            return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.NotFound);
        }
    }
}

internal sealed class ActivateFasSchemeDraftHandler(
    IFasSchemeRepository repository, ICourseReferenceDirectory courses, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<ActivateFasSchemeDraftCommand, CreateFasSchemeResponse>
{
    public async Task<Result<CreateFasSchemeResponse>> Handle(ActivateFasSchemeDraftCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is not long actorId) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.Unauthenticated);
        if (await repository.SchemeCodeExistsExcludingAsync(command.Request.SchemeCode, command.SchemeId, cancellationToken)) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.DuplicateSchemeCode);
        if (await repository.GrantCodeExistsExcludingAsync(command.Request.GrantCode, command.SchemeId, cancellationToken)) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.DuplicateGrantCode);
        IReadOnlyList<long> unknown = await courses.FindUnknownCourseIdsAsync(command.Request.CourseIds.Distinct().ToArray(), cancellationToken);
        if (unknown.Count > 0) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.UnknownCourses(unknown));
        return Result<CreateFasSchemeResponse>.Success(await repository.ActivateDraftAsync(command.SchemeId, command.Request, actorId, clock.UtcNow.UtcDateTime, cancellationToken));
    }
}

internal sealed class DeleteFasSchemeDraftHandler(IFasSchemeRepository repository, ICurrentUser currentUser)
    : ICommandHandler<DeleteFasSchemeDraftCommand, bool>
{
    public async Task<Result<bool>> Handle(DeleteFasSchemeDraftCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is null) return Result<bool>.Failure(FasSchemeErrors.Unauthenticated);
        bool deleted = await repository.DeleteDraftAsync(command.SchemeId, cancellationToken);
        return deleted ? Result<bool>.Success(true) : Result<bool>.Failure(FasSchemeErrors.NotFound);
    }
}

internal sealed class ListFasSchemesHandler(IFasSchemeRepository repository) : IQueryHandler<ListFasSchemesQuery, FasSchemeListResponse>
{
    public async Task<Result<FasSchemeListResponse>> Handle(ListFasSchemesQuery query, CancellationToken cancellationToken)
        => Result<FasSchemeListResponse>.Success(await repository.ListAsync(query.Status, query.Search, cancellationToken));
}

internal sealed class GetFasSchemeHandler(IFasSchemeRepository repository) : IQueryHandler<GetFasSchemeQuery, FasSchemeDetail>
{
    public async Task<Result<FasSchemeDetail>> Handle(GetFasSchemeQuery query, CancellationToken cancellationToken)
    {
        FasSchemeDetail? scheme = await repository.GetAsync(query.SchemeId, cancellationToken);
        return scheme is null ? Result<FasSchemeDetail>.Failure(FasSchemeErrors.NotFound) : Result<FasSchemeDetail>.Success(scheme);
    }
}
