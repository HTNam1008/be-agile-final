using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.IGateway.Courses;
using Moe.Modules.FasPayment.Application.Audit;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;
using Moe.Modules.FasPayment.IGateway.Repositories;
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
    IClock clock,
    IFasSchoolAuditResolver schoolResolver,
    IAuditService audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateFasSchemeCommand, CreateFasSchemeResponse>
{
    public async Task<Result<CreateFasSchemeResponse>> Handle(CreateFasSchemeCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is not long actorId) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.Unauthenticated);
        CreateFasSchemeRequest request = FasSchemeRequestDefaults.WithSystemGrantCode(command.Request);
        if (await repository.SchemeCodeExistsAsync(request.SchemeCode, cancellationToken)) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.DuplicateSchemeCode);
        if (await repository.GrantCodeExistsAsync(request.GrantCode, cancellationToken)) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.DuplicateGrantCode);
        IReadOnlyList<long> unknown = await courses.FindUnknownCourseIdsAsync(request.CourseIds.Distinct().ToArray(), cancellationToken);
        if (unknown.Count > 0) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.UnknownCourses(unknown));
        try
        {
            CreateFasSchemeResponse response = await repository.CreateAsync(request, actorId, clock.UtcNow.UtcDateTime, cancellationToken);
            await FasSchemeAuditWriter.RecordForCourseTargetsAsync(
                schoolResolver,
                audit,
                unitOfWork,
                AuditActionCodes.FasSchemeCreated,
                response,
                request,
                "FAS scheme created",
                cancellationToken);

            return Result<CreateFasSchemeResponse>.Success(response);
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
    IFasSchemeRepository repository,
    ICourseReferenceDirectory courses,
    ICurrentUser currentUser,
    IClock clock,
    IFasSchoolAuditResolver schoolResolver,
    IAuditService audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SaveFasSchemeDraftCommand, CreateFasSchemeResponse>
{
    public async Task<Result<CreateFasSchemeResponse>> Handle(SaveFasSchemeDraftCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is not long actorId) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.Unauthenticated);
        CreateFasSchemeRequest request = FasSchemeRequestDefaults.WithSystemGrantCode(command.Request);
        bool duplicateScheme = command.SchemeId.HasValue
            ? await repository.SchemeCodeExistsExcludingAsync(request.SchemeCode, command.SchemeId.Value, cancellationToken)
            : await repository.SchemeCodeExistsAsync(request.SchemeCode, cancellationToken);
        bool duplicateGrant = command.SchemeId.HasValue
            ? await repository.GrantCodeExistsExcludingAsync(request.GrantCode, command.SchemeId.Value, cancellationToken)
            : await repository.GrantCodeExistsAsync(request.GrantCode, cancellationToken);
        if (duplicateScheme) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.DuplicateSchemeCode);
        if (duplicateGrant) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.DuplicateGrantCode);
        IReadOnlyList<long> unknown = await courses.FindUnknownCourseIdsAsync(request.CourseIds.Distinct().ToArray(), cancellationToken);
        if (unknown.Count > 0) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.UnknownCourses(unknown));
        try
        {
            CreateFasSchemeResponse response = await repository.SaveDraftAsync(command.SchemeId, request, actorId, clock.UtcNow.UtcDateTime, cancellationToken);
            await FasSchemeAuditWriter.RecordForCourseTargetsAsync(
                schoolResolver,
                audit,
                unitOfWork,
                AuditActionCodes.FasSchemeUpdated,
                response,
                request,
                "FAS scheme edited",
                cancellationToken);

            return Result<CreateFasSchemeResponse>.Success(response);
        }
        catch (InvalidOperationException) when (command.SchemeId.HasValue)
        {
            return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.NotFound);
        }
    }
}

internal sealed class ActivateFasSchemeDraftHandler(
    IFasSchemeRepository repository,
    ICourseReferenceDirectory courses,
    ICurrentUser currentUser,
    IClock clock,
    IFasSchoolAuditResolver schoolResolver,
    IAuditService audit,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivateFasSchemeDraftCommand, CreateFasSchemeResponse>
{
    public async Task<Result<CreateFasSchemeResponse>> Handle(ActivateFasSchemeDraftCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is not long actorId) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.Unauthenticated);
        CreateFasSchemeRequest request = FasSchemeRequestDefaults.WithSystemGrantCode(command.Request);
        if (await repository.SchemeCodeExistsExcludingAsync(request.SchemeCode, command.SchemeId, cancellationToken)) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.DuplicateSchemeCode);
        if (await repository.GrantCodeExistsExcludingAsync(request.GrantCode, command.SchemeId, cancellationToken)) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.DuplicateGrantCode);
        IReadOnlyList<long> unknown = await courses.FindUnknownCourseIdsAsync(request.CourseIds.Distinct().ToArray(), cancellationToken);
        if (unknown.Count > 0) return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.UnknownCourses(unknown));
        CreateFasSchemeResponse response = await repository.ActivateDraftAsync(command.SchemeId, request, actorId, clock.UtcNow.UtcDateTime, cancellationToken);
        await FasSchemeAuditWriter.RecordForCourseTargetsAsync(
            schoolResolver,
            audit,
            unitOfWork,
            AuditActionCodes.FasSchemePublished,
            response,
            request,
            "FAS scheme published",
            cancellationToken);

        return Result<CreateFasSchemeResponse>.Success(response);
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

internal abstract class FasSchemeLifecycleHandlerBase(
    IFasSchemeRepository repository,
    ICurrentUser currentUser,
    IClock clock,
    IFasSchoolAuditResolver schoolResolver,
    IAuditService audit,
    IUnitOfWork unitOfWork)
{
    protected IFasSchemeRepository Repository { get; } = repository;

    protected async Task<Result<CreateFasSchemeResponse>> Execute(
        long schemeId,
        Func<long, long, DateTime, CancellationToken, Task<CreateFasSchemeResponse?>> operation,
        string actionCode,
        string summary,
        string? beforeStatus,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is not long actorId)
            return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.Unauthenticated);

        try
        {
            CreateFasSchemeResponse? result = await operation(schemeId, actorId, clock.UtcNow.UtcDateTime, cancellationToken);
            if (result is null)
            {
                return Result<CreateFasSchemeResponse>.Failure(FasSchemeErrors.NotFound);
            }

            await FasSchemeAuditWriter.RecordForSchemeTargetsAsync(
                schoolResolver,
                audit,
                unitOfWork,
                actionCode,
                result,
                summary,
                beforeStatus,
                cancellationToken);

            return Result<CreateFasSchemeResponse>.Success(result);
        }
        catch (InvalidOperationException exception)
        {
            return Result<CreateFasSchemeResponse>.Failure(new Error("FAS.INVALID_STATUS_TRANSITION", exception.Message));
        }
    }
}

internal sealed class PublishFasSchemeHandler(
    IFasSchemeRepository repository,
    ICurrentUser currentUser,
    IClock clock,
    IFasSchoolAuditResolver schoolResolver,
    IAuditService audit,
    IUnitOfWork unitOfWork)
    : FasSchemeLifecycleHandlerBase(repository, currentUser, clock, schoolResolver, audit, unitOfWork), ICommandHandler<PublishFasSchemeCommand, CreateFasSchemeResponse>
{
    public Task<Result<CreateFasSchemeResponse>> Handle(PublishFasSchemeCommand command, CancellationToken cancellationToken)
        => Execute(command.SchemeId, Repository.PublishAsync, AuditActionCodes.FasSchemePublished, "FAS scheme published", "DRAFT", cancellationToken);
}

internal sealed class DisableFasSchemeHandler(
    IFasSchemeRepository repository,
    ICurrentUser currentUser,
    IClock clock,
    IFasSchoolAuditResolver schoolResolver,
    IAuditService audit,
    IUnitOfWork unitOfWork)
    : FasSchemeLifecycleHandlerBase(repository, currentUser, clock, schoolResolver, audit, unitOfWork), ICommandHandler<DisableFasSchemeCommand, CreateFasSchemeResponse>
{
    public Task<Result<CreateFasSchemeResponse>> Handle(DisableFasSchemeCommand command, CancellationToken cancellationToken)
        => Execute(command.SchemeId, Repository.DisableAsync, AuditActionCodes.FasSchemeDisabled, "FAS scheme disabled", "ACTIVE", cancellationToken);
}

internal sealed class DeleteFasSchemeHandler(
    IFasSchemeRepository repository,
    ICurrentUser currentUser,
    IClock clock,
    IFasSchoolAuditResolver schoolResolver,
    IAuditService audit,
    IUnitOfWork unitOfWork)
    : FasSchemeLifecycleHandlerBase(repository, currentUser, clock, schoolResolver, audit, unitOfWork), ICommandHandler<DeleteFasSchemeCommand, CreateFasSchemeResponse>
{
    public Task<Result<CreateFasSchemeResponse>> Handle(DeleteFasSchemeCommand command, CancellationToken cancellationToken)
        => Execute(command.SchemeId, Repository.DeleteAsync, AuditActionCodes.FasSchemeDeleted, "FAS scheme deleted", null, cancellationToken);
}

internal static class FasSchemeAuditWriter
{
    public static async Task RecordForCourseTargetsAsync(
        IFasSchoolAuditResolver resolver,
        IAuditService audit,
        IUnitOfWork unitOfWork,
        string actionCode,
        CreateFasSchemeResponse scheme,
        CreateFasSchemeRequest request,
        string summary,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<long> schoolIds = await resolver.ResolveFromCourseIdsAsync(request.CourseIds, cancellationToken);
        await RecordAsync(audit, unitOfWork, actionCode, scheme, summary, null, schoolIds, cancellationToken);
    }

    public static async Task RecordForSchemeTargetsAsync(
        IFasSchoolAuditResolver resolver,
        IAuditService audit,
        IUnitOfWork unitOfWork,
        string actionCode,
        CreateFasSchemeResponse scheme,
        string summary,
        string? beforeStatus,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<long> schoolIds = await resolver.ResolveForSchemeAsync(scheme.SchemeId, cancellationToken);
        await RecordAsync(audit, unitOfWork, actionCode, scheme, summary, beforeStatus, schoolIds, cancellationToken);
    }

    private static async Task RecordAsync(
        IAuditService audit,
        IUnitOfWork unitOfWork,
        string actionCode,
        CreateFasSchemeResponse scheme,
        string summary,
        string? beforeStatus,
        IReadOnlyCollection<long> schoolIds,
        CancellationToken cancellationToken)
    {
        if (schoolIds.Count == 0)
        {
            return;
        }

        foreach (long schoolId in schoolIds)
        {
            await audit.RecordSchoolActionAsync(
                new SchoolAuditContext(
                    actionCode,
                    "FasScheme",
                    scheme.SchemeId,
                    schoolId,
                    new SchoolAuditDetails(
                        summary,
                        EntityDisplayName: scheme.SchemeCode,
                        RelatedIds: new Dictionary<string, long> { ["schemeId"] = scheme.SchemeId },
                        StatusTransition: new SchoolAuditStatusTransition(beforeStatus, scheme.Status))),
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class ListFasSchemesHandler(IFasSchemeRepository repository) : IQueryHandler<ListFasSchemesQuery, PageResponse<FasSchemeListItem>>
{
    public async Task<Result<PageResponse<FasSchemeListItem>>> Handle(ListFasSchemesQuery query, CancellationToken cancellationToken)
        => Result<PageResponse<FasSchemeListItem>>.Success(await repository.ListAsync(query.Status, query.Search, query.Page, query.PageSize, query.SortBy, query.SortDirection, cancellationToken));
}

internal sealed class GetFasSchemeHandler(IFasSchemeRepository repository) : IQueryHandler<GetFasSchemeQuery, FasSchemeDetail>
{
    public async Task<Result<FasSchemeDetail>> Handle(GetFasSchemeQuery query, CancellationToken cancellationToken)
    {
        FasSchemeDetail? scheme = await repository.GetAsync(query.SchemeId, cancellationToken);
        return scheme is null ? Result<FasSchemeDetail>.Failure(FasSchemeErrors.NotFound) : Result<FasSchemeDetail>.Success(scheme);
    }
}
