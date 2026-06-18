using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.CourseBilling.Api.Admin;
using Moe.Modules.CourseBilling.Api.EService;
using Moe.Modules.CourseBilling.Application.Enrollments;
using Moe.Modules.CourseBilling.Application.Enrollments.AdminEnrollPerson;
using Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;
using Moe.Modules.CourseBilling.IGateway.Repositories;
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
        services.AddScoped<ICommandHandler<AdminEnrollPersonCommand, CourseEnrollmentResponse>, AdminEnrollPersonHandler>();
        services.AddScoped<ICommandHandler<SelfJoinCourseCommand, CourseEnrollmentResponse>, SelfJoinCourseHandler>();
        services.AddScoped<IValidator<AdminEnrollPersonRequest>, AdminEnrollPersonRequestValidator>();
        services.AddScoped<IValidator<SelfJoinCourseRequest>, SelfJoinCourseRequestValidator>();
        
        services.AddScoped<IAdminCourseRepository, AdminCourseRepository>();
services.AddScoped<IAdminFeeComponentRepository, AdminFeeComponentRepository>();
services.AddScoped<ICourseMaterialStorageService, LocalCourseMaterialStorageService>();
services.AddScoped<ICurrentAdminContext, CurrentAdminContext>();

services.AddScoped<IAdminCourseService, AdminCourseService>();
services.AddScoped<IAdminFeeComponentService, AdminFeeComponentService>();

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
