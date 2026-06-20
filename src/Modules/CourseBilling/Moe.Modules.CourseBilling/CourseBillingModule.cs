using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Api.Admin;
using Moe.Modules.CourseBilling.Api.EService;
using Moe.Modules.CourseBilling.Application.AdminCourses;
using Moe.Modules.CourseBilling.Application.AdminCourses.Courses;
using Moe.Modules.CourseBilling.Application.AdminCourses.Enrollments;
using Moe.Modules.CourseBilling.Application.AdminCourses.Fees;
using Moe.Modules.CourseBilling.Application.AdminCourses.Materials;
using Moe.Modules.CourseBilling.Application.AdminFeeComponents;
using Moe.Modules.CourseBilling.Application.Enrollments.AdminEnrollPerson;
using Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;
using Moe.Modules.CourseBilling.Contracts.AdminEnrollments;
using Moe.Modules.CourseBilling.Contracts.AdminFeeComponents;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Contracts.Enrollments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.IGateway.Storage;
using Moe.Modules.CourseBilling.Infrastructure.Repositories;

using Moe.Modules.CourseBilling.Application.AdminCourses;
using Moe.Modules.CourseBilling.Application.AdminFeeComponents;
using Moe.Modules.CourseBilling.IGateway.Storage;
using Moe.Modules.CourseBilling.Infrastructure.Security;
using Moe.Modules.CourseBilling.Infrastructure.Storage;
namespace Moe.Modules.CourseBilling;

public sealed class CourseBillingModule : IModule
{
    public string Name => "CourseBilling";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelConfigurationContributor, CourseBillingModelConfiguration>();

        services.AddScoped<ICourseEnrollmentRepository, CourseEnrollmentRepository>();
        services.AddScoped<IAdminCourseRepository, AdminCourseRepository>();
        services.AddScoped<IAdminFeeComponentRepository, AdminFeeComponentRepository>();

        services.AddScoped<AdminCourseAccess>();
        services.AddScoped<ICurrentAdminContext, CurrentAdminContext>();
        services.AddScoped<ICourseMaterialStorageService, LocalCourseMaterialStorageService>();

        services.AddScoped<IQueryHandler<ListCoursesQuery, PageResponse<CourseSummaryDto>>, ListCoursesQueryHandler>();
        services.AddScoped<IQueryHandler<GetCourseQuery, CourseDetailDto>, GetCourseQueryHandler>();
        services.AddScoped<IQueryHandler<PreviewCourseQuery, CoursePreviewDto>, PreviewCourseQueryHandler>();
        services.AddScoped<ICommandHandler<CreateCourseCommand, CourseDetailDto>, CreateCourseCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateCourseCommand, CourseDetailDto>, UpdateCourseCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveCourseCommand, long>, RemoveCourseCommandHandler>();
        services.AddScoped<ICommandHandler<PublishCourseCommand, CourseDetailDto>, PublishCourseCommandHandler>();
        services.AddScoped<ICommandHandler<DisableCourseCommand, CourseDetailDto>, DisableCourseCommandHandler>();
        services.AddScoped<ICommandHandler<EnableCourseCommand, CourseDetailDto>, EnableCourseCommandHandler>();

        services.AddScoped<IQueryHandler<ListCourseMaterialsQuery, IReadOnlyList<CourseMaterialDto>>, ListCourseMaterialsQueryHandler>();
        services.AddScoped<ICommandHandler<AddCourseMaterialCommand, CourseMaterialDto>, AddCourseMaterialCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateCourseMaterialCommand, CourseMaterialDto>, UpdateCourseMaterialCommandHandler>();
        services.AddScoped<ICommandHandler<ReplaceCourseMaterialFileCommand, CourseMaterialDto>, ReplaceCourseMaterialFileCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteCourseMaterialCommand, CourseMaterialDto>, DeleteCourseMaterialCommandHandler>();

        services.AddScoped<IQueryHandler<ListCourseFeesQuery, IReadOnlyList<CourseFeeDto>>, ListCourseFeesQueryHandler>();
        services.AddScoped<ICommandHandler<AddCourseFeeCommand, CourseFeeDto>, AddCourseFeeCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateCourseFeeCommand, CourseFeeDto>, UpdateCourseFeeCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteCourseFeeCommand, CourseFeeDto>, DeleteCourseFeeCommandHandler>();

        services.AddScoped<IQueryHandler<ListAdminCourseEnrollmentsQuery, IReadOnlyList<AdminCourseEnrollmentDto>>, ListAdminCourseEnrollmentsQueryHandler>();
        services.AddScoped<ICommandHandler<RemoveAdminCourseEnrollmentCommand, AdminCourseEnrollmentDto>, RemoveAdminCourseEnrollmentCommandHandler>();

        services.AddScoped<IQueryHandler<ListFeeComponentsQuery, PageResponse<FeeComponentDto>>, ListFeeComponentsQueryHandler>();
        services.AddScoped<IQueryHandler<GetFeeComponentQuery, FeeComponentDto>, GetFeeComponentQueryHandler>();
        services.AddScoped<ICommandHandler<CreateFeeComponentCommand, FeeComponentDto>, CreateFeeComponentCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateFeeComponentCommand, FeeComponentDto>, UpdateFeeComponentCommandHandler>();
        services.AddScoped<ICommandHandler<ActivateFeeComponentCommand, FeeComponentDto>, ActivateFeeComponentCommandHandler>();
        services.AddScoped<ICommandHandler<DeactivateFeeComponentCommand, FeeComponentDto>, DeactivateFeeComponentCommandHandler>();

        services.AddScoped<ICommandHandler<AdminEnrollPersonCommand, CourseEnrollmentResponse>, AdminEnrollPersonHandler>();
        services.AddScoped<ICommandHandler<SelfJoinCourseCommand, CourseEnrollmentResponse>, SelfJoinCourseHandler>();

        services.AddScoped<IValidator<AdminEnrollPersonRequest>, AdminEnrollPersonRequestValidator>();
        services.AddScoped<IValidator<SelfJoinCourseRequest>, SelfJoinCourseRequestValidator>();
        services.AddScoped<IValidator<CreateCourseRequest>, CreateCourseRequestValidator>();
        services.AddScoped<IValidator<UpdateCourseRequest>, UpdateCourseRequestValidator>();
        services.AddScoped<IValidator<CreateCourseMaterialRequest>, CreateCourseMaterialRequestValidator>();
        services.AddScoped<IValidator<UpdateCourseMaterialRequest>, UpdateCourseMaterialRequestValidator>();
        services.AddScoped<IValidator<CreateCourseFeeRequest>, CreateCourseFeeRequestValidator>();
        services.AddScoped<IValidator<UpdateCourseFeeRequest>, UpdateCourseFeeRequestValidator>();
        services.AddScoped<IValidator<CreateFeeComponentRequest>, CreateFeeComponentRequestValidator>();
        services.AddScoped<IValidator<UpdateFeeComponentRequest>, UpdateFeeComponentRequestValidator>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
