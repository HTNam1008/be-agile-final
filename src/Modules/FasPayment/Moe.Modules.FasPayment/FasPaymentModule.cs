using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.FasPayment.Application.AdminFasSchemes;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;
using Moe.Modules.FasPayment.Application.Applications.Approve;
using Moe.Modules.FasPayment.Application.Applications.GetApplicationDetail;
using Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications;
using Moe.Modules.FasPayment.Application.Applications.Reject;
using Moe.Modules.FasPayment.Application.AdminPayments;
using Moe.Modules.FasPayment.Application.Checkout;
using Moe.Modules.FasPayment.Application.LegacyPayments;
using Moe.Modules.FasPayment.Application.PaymentPlans;
using Moe.Modules.FasPayment.Application.StatementPayments;
using Moe.Modules.FasPayment.Application.Webhooks;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.Modules.FasPayment.Infrastructure.Payments;
using Moe.Modules.FasPayment.Infrastructure.Repositories;
using Moe.Modules.FasPayment.Infrastructure.Stripe;
using Moe.Modules.CourseBilling.IGateway.Payments;

namespace Moe.Modules.FasPayment;

public sealed class FasPaymentModule : IModule
{
    public string Name => "FasPayment";
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelConfigurationContributor, FasPaymentModelConfiguration>();
        services.AddScoped<IFasSchemeRepository, FasSchemeRepository>();
        services.AddScoped<ICommandHandler<CreateFasSchemeCommand, CreateFasSchemeResponse>, CreateFasSchemeHandler>();
        services.AddScoped<ICommandHandler<SaveFasSchemeDraftCommand, CreateFasSchemeResponse>, SaveFasSchemeDraftHandler>();
        services.AddScoped<ICommandHandler<ActivateFasSchemeDraftCommand, CreateFasSchemeResponse>, ActivateFasSchemeDraftHandler>();
        services.AddScoped<ICommandHandler<DeleteFasSchemeDraftCommand, bool>, DeleteFasSchemeDraftHandler>();
        services.AddScoped<IQueryHandler<ListFasSchemesQuery, FasSchemeListResponse>, ListFasSchemesHandler>();
        services.AddScoped<IQueryHandler<GetFasSchemeQuery, FasSchemeDetail>, GetFasSchemeHandler>();
        services.AddScoped<IValidator<CreateFasSchemeRequest>, CreateFasSchemeRequestValidator>();
        services.AddScoped<IValidator<ListFasSchemesRequest>, ListFasSchemesRequestValidator>();
        services.AddScoped<IFasApplicationRepository, FasApplicationRepository>();

        services.AddScoped<IQueryHandler<GetSchemeApplicationsQuery, GetSchemeApplicationsResponse>, GetSchemeApplicationsHandler>();
        services.AddScoped<IQueryHandler<GetApplicationDetailQuery, GetApplicationDetailResponse>, GetApplicationDetailHandler>();
        services.AddScoped<ICommandHandler<ApproveApplicationCommand, ApproveApplicationResponse>, ApproveApplicationHandler>();
        services.AddScoped<ICommandHandler<RejectApplicationCommand, RejectApplicationResponse>, RejectApplicationHandler>();

        services.AddOptions<StripePaymentOptions>().BindConfiguration(StripePaymentOptions.SectionName);
        services.AddScoped<IPaymentCheckoutRepository, PaymentCheckoutRepository>();
        services.AddScoped<IPaymentPersistenceTracker, PaymentPersistenceTracker>();
        services.AddScoped<IStripePaymentGateway, StripePaymentGateway>();
        services.AddScoped<ILegacyCoursePaymentGateway, LegacyCoursePaymentGateway>();
        services.AddSingleton<IStripeWebhookCoordinator, StripeWebhookCoordinator>();
        services.AddScoped<ICoursePaymentPlanGateway, CoursePaymentPlanGateway>();
        services.AddScoped<ICommandHandler<CreateCoursePaymentPlanCommand, CoursePaymentPlanResponse>, CreateCoursePaymentPlanHandler>();
        services.AddScoped<IQueryHandler<ListCoursePaymentPlansQuery, IReadOnlyCollection<CoursePaymentPlanResponse>>, ListCoursePaymentPlansHandler>();
        services.AddScoped<ICommandHandler<CreateStripeCheckoutCommand, StripeCheckoutResponse>, CreateStripeCheckoutHandler>();
        services.AddScoped<IQueryHandler<GetPaymentCheckoutStatusQuery, PaymentCheckoutStatusResponse>, GetPaymentCheckoutStatusHandler>();
        services.AddScoped<ICommandHandler<ProcessStripeWebhookCommand>, ProcessStripeWebhookHandler>();
        services.AddScoped<IQueryHandler<GetOutstandingBillsQuery, OutstandingBillsResponse>, GetOutstandingBillsHandler>();
        services.AddScoped<ICommandHandler<PayOutstandingBillCommand, PayBillResponse>, PayOutstandingBillHandler>();
        services.AddScoped<IQueryHandler<ListAdminPaymentsQuery, IReadOnlyCollection<AdminPaymentResponse>>, ListAdminPaymentsHandler>();
        services.AddScoped<IQueryHandler<ListPaymentWebhookEventsQuery, IReadOnlyCollection<PaymentWebhookEventResponse>>, ListPaymentWebhookEventsHandler>();
        services.AddScoped<ICommandHandler<CreatePaymentRefundCommand, PaymentRefundResponse>, CreatePaymentRefundHandler>();
        services.AddScoped<IQueryHandler<PreviewStatementPaymentQuery, StatementPaymentPreviewResponse>, PreviewStatementPaymentHandler>();
        services.AddScoped<ICommandHandler<PayBillingStatementCommand, PayBillingStatementResponse>, PayBillingStatementHandler>();
        services.AddScoped<ICommandHandler<DeferBillingStatementCommand>, DeferBillingStatementHandler>();
        services.AddScoped<IQueryHandler<ListUserPaymentHistoryQuery, IReadOnlyCollection<UserPaymentHistoryResponse>>, ListUserPaymentHistoryHandler>();
        services.AddScoped<IValidator<CreateCoursePaymentPlanRequest>, CreateCoursePaymentPlanRequestValidator>();
        services.AddScoped<IValidator<CreateStripeCheckoutRequest>, CreateStripeCheckoutRequestValidator>();
        services.AddScoped<IValidator<PayBillRequest>, PayBillRequestValidator>();
        services.AddScoped<IValidator<CreatePaymentRefundRequest>, CreatePaymentRefundRequestValidator>();
        services.AddScoped<IValidator<PayBillingStatementRequest>, PayBillingStatementRequestValidator>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
