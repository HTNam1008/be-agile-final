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
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
