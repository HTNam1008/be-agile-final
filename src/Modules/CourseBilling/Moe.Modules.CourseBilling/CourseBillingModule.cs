using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
using Moe.Modules.CourseBilling.Application.AdminStudentCourses;
using Moe.Modules.CourseBilling.Application.BillingStatements;
using Moe.Modules.CourseBilling.Application.Dashboard.GetAdminDashboard;
using Moe.Modules.CourseBilling.Application.Dashboard.GetStudentDashboard;
using Moe.Modules.CourseBilling.Application.Dashboard.RoleDashboards;
using Moe.Modules.CourseBilling.Application.Enrollments.AdminEnrollPerson;
using Moe.Modules.CourseBilling.Application.Enrollments.CourseContent;
using Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Contracts.AdminEnrollments;
using Moe.Modules.CourseBilling.Contracts.AdminFeeComponents;
using Moe.Modules.CourseBilling.Contracts.BillingStatements;
using Moe.Modules.CourseBilling.Contracts.Enrollments;
using Moe.Modules.CourseBilling.IGateway.AdminStudentCourses;
using Moe.Modules.CourseBilling.IGateway.Courses;
using Moe.Modules.CourseBilling.IGateway.Dashboard;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.IGateway.Storage;
using Moe.Modules.CourseBilling.Infrastructure.AdminStudentCourses;
using Moe.Modules.CourseBilling.Infrastructure.BillingStatements;
using Moe.Modules.CourseBilling.Infrastructure;
using Moe.Modules.CourseBilling.Infrastructure.Courses;
using Moe.Modules.CourseBilling.Infrastructure.Dashboard;
using Moe.Modules.CourseBilling.Infrastructure.Payments;
using Moe.Modules.CourseBilling.Infrastructure.Repositories;
using Moe.Modules.CourseBilling.Infrastructure.Security;
using Moe.Modules.CourseBilling.Infrastructure.Storage;
using ContractCreateFeeComponentRequest = Moe.Modules.CourseBilling.Contracts.AdminFeeComponents.CreateFeeComponentRequest;
using ContractUpdateFeeComponentRequest = Moe.Modules.CourseBilling.Contracts.AdminFeeComponents.UpdateFeeComponentRequest;

namespace Moe.Modules.CourseBilling;

public sealed class CourseBillingModule : IModule
{
    public string Name => "CourseBilling";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelConfigurationContributor, CourseBillingModelConfiguration>();
        services.AddOptions<CourseBillingWorkerOptions>()
            .BindConfiguration(CourseBillingWorkerOptions.SectionName);

        services.AddScoped<ICourseEnrollmentRepository, CourseEnrollmentRepository>();
        services.AddScoped<ICoursePaymentGateway, CoursePaymentGateway>();
        if (IsBackgroundJobEnabled(configuration, "CourseBilling:MonthlyBillNotifications"))
        {
            services.AddHostedService<MonthlyBillNotificationWorker>();
        }
        if (IsBackgroundJobEnabled(configuration, "CourseBilling:MissedInstallmentPaymentEmails"))
        {
            services.AddHostedService<MissedInstallmentPaymentEmailWorker>();
        }
        services.AddScoped<IAdminCourseRepository, AdminCourseRepository>();
        services.AddScoped<IAdminFeeComponentRepository, AdminFeeComponentRepository>();
        services.AddScoped<IAdminDashboardCourseRepository, AdminDashboardCourseRepository>();
        services.AddScoped<IAdminDashboardCourseMetricsReader, AdminDashboardCourseMetricsReader>();
        services.AddScoped<IStudentDashboardCourseRepository, StudentDashboardCourseRepository>();
        services.AddScoped<IAdminStudentEnrolledCourseReader, AdminStudentEnrolledCourseReader>();
        services.AddScoped<IStudentCourseContentRepository, StudentCourseContentRepository>();
        services.AddScoped<ICourseReferenceDirectory, CourseReferenceDirectory>();
        services.AddScoped<IBillingStatementRepository, BillingStatementRepository>();
        services.AddScoped<AdminCourseAccess>();
        services.AddScoped<ICurrentAdminContext, CurrentAdminContext>();
        services.AddScoped<ICourseMaterialStorageService, AzureBlobCourseMaterialStorageService>();
        services.AddScoped<ICourseMaterialPreviewConverter, PowerPointCourseMaterialPreviewConverter>();
        services.AddSingleton<ICourseMaterialPreviewRedisCache, RedisCourseMaterialPreviewCache>();
        services.AddSingleton<ICourseMaterialPreviewBlobCache, AzureBlobCourseMaterialPreviewCache>();
        services.AddSingleton<ICourseMaterialPreviewCache, CompositeCourseMaterialPreviewCache>();

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
        services.AddScoped<ICommandHandler<DeleteFeeComponentCommand, long>, DeleteFeeComponentCommandHandler>();

        services.AddScoped<ICommandHandler<AdminEnrollPersonCommand, CourseEnrollmentResponse>, AdminEnrollPersonHandler>();
        services.AddScoped<PaymentPlanBillPreviewBuilder>();
        services.AddScoped<ICommandHandler<SelfJoinCourseCommand, CourseEnrollmentResponse>, SelfJoinCourseHandler>();
        services.AddScoped<ICommandHandler<ChangeEnrollmentPaymentPlanCommand, CourseEnrollmentResponse>, ChangeEnrollmentPaymentPlanHandler>();
        services.AddScoped<IQueryHandler<PreviewCoursePaymentPlanBillQuery, PaymentPlanBillPreviewResponse>, PreviewCoursePaymentPlanBillHandler>();
        services.AddScoped<IQueryHandler<PreviewPaymentPlanBillQuery, PaymentPlanBillPreviewResponse>, PreviewPaymentPlanBillHandler>();
        services.AddScoped<IQueryHandler<GetStudentCourseContentQuery, StudentCourseContentResponse>, GetStudentCourseContentHandler>();
        services.AddScoped<IQueryHandler<DownloadStudentCourseMaterialQuery, StudentCourseMaterialDownload>, DownloadStudentCourseMaterialHandler>();
        services.AddScoped<IQueryHandler<GetStudentCourseMaterialOfficePreviewQuery, StudentCourseMaterialOfficePreviewResponse>, GetStudentCourseMaterialOfficePreviewHandler>();
        services.AddScoped<IQueryHandler<GetAdminDashboardQuery, AdminDashboardResponse>, GetAdminDashboardHandler>();
        services.AddScoped<IQueryHandler<GetHqDashboardQuery, HqDashboardResponse>, GetHqDashboardHandler>();
        services.AddScoped<IQueryHandler<GetSchoolDashboardQuery, SchoolDashboardResponse>, GetSchoolDashboardHandler>();
        services.AddScoped<IQueryHandler<GetStudentDashboardQuery, StudentDashboardResponse>, GetStudentDashboardHandler>();
        services.AddScoped<IQueryHandler<GetStudentEnrolledCoursesQuery, PageResponse<AdminStudentEnrolledCourseItem>>, GetStudentEnrolledCoursesHandler>();
        services.AddScoped<IQueryHandler<GetStudentDashboardSummaryQuery, StudentDashboardSummaryResponse>, GetStudentDashboardSummaryHandler>();
        services.AddScoped<IQueryHandler<GetStudentCoursesQuery, StudentCoursesResponse>, GetStudentCoursesHandler>();
        services.AddScoped<IQueryHandler<GetBillingStatementQuery, BillingStatementResponse>, GetBillingStatementHandler>();

        services.AddScoped<IValidator<AdminEnrollPersonRequest>, AdminEnrollPersonRequestValidator>();
        services.AddScoped<IValidator<SelfJoinCourseRequest>, SelfJoinCourseRequestValidator>();
        services.AddScoped<IValidator<PreviewCoursePaymentPlanBillRequest>, PreviewCoursePaymentPlanBillRequestValidator>();
        services.AddScoped<IValidator<CreateCourseRequest>, CreateCourseRequestValidator>();
        services.AddScoped<IValidator<UpdateCourseRequest>, UpdateCourseRequestValidator>();
        services.AddScoped<IValidator<CreateCourseMaterialRequest>, CreateCourseMaterialRequestValidator>();
        services.AddScoped<IValidator<UpdateCourseMaterialRequest>, UpdateCourseMaterialRequestValidator>();
        services.AddScoped<IValidator<CreateCourseFeeRequest>, CreateCourseFeeRequestValidator>();
        services.AddScoped<IValidator<UpdateCourseFeeRequest>, UpdateCourseFeeRequestValidator>();
        services.AddScoped<IValidator<ContractCreateFeeComponentRequest>, CreateFeeComponentRequestValidator>();
        services.AddScoped<IValidator<ContractUpdateFeeComponentRequest>, UpdateFeeComponentRequestValidator>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }

    private static bool IsBackgroundJobEnabled(IConfiguration configuration, string key)
        => configuration.GetValue("BackgroundJobs:Enabled", true)
           && configuration.GetValue($"BackgroundJobs:{key}", true);
}
