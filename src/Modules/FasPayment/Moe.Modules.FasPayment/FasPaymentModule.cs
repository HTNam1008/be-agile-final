using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.FasPayment.Api;
using Moe.Modules.FasPayment.Application.AdminFasSchemes;
using Moe.Modules.FasPayment.Application.AdminPayments;
using Moe.Modules.FasPayment.Application.Audit;
using Moe.Modules.FasPayment.Application.Applications.Approve;
using Moe.Modules.FasPayment.Application.Applications.GetApplicationDetail;
using Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications;
using Moe.Modules.FasPayment.Application.Applications.Reject;
using Moe.Modules.FasPayment.Application.Checkout;
using Moe.Modules.FasPayment.Application.EnrollmentCancellations;
using Moe.Modules.FasPayment.Application.Notifications;
using Moe.Modules.FasPayment.Application.PaymentPlans;
using Moe.Modules.FasPayment.Application.StatementPayments;
using Moe.Modules.FasPayment.Application.StudentApplications;
using Moe.Modules.FasPayment.Application.Webhooks;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.Modules.FasPayment.Infrastructure.Documents;
using Moe.Modules.FasPayment.Infrastructure.Audit;
using Moe.Modules.FasPayment.Infrastructure.Payments;
using Moe.Modules.FasPayment.Infrastructure.Repositories;
using Moe.Modules.FasPayment.Infrastructure.Stripe;

namespace Moe.Modules.FasPayment;

public sealed class FasPaymentModule : IModule
{
    public string Name => "FasPayment";
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelConfigurationContributor, FasPaymentModelConfiguration>();
        services.AddScoped<IFasSchemeRepository, FasSchemeRepository>();
        services.AddScoped<IFasSchoolAuditResolver, FasSchoolAuditResolver>();
        services.AddScoped<IFasCourseSubsidyGateway, FasCourseSubsidyGateway>();
        services.AddScoped<ICommandHandler<CreateFasSchemeCommand, CreateFasSchemeResponse>, CreateFasSchemeHandler>();
        services.AddScoped<ICommandHandler<SaveFasSchemeDraftCommand, CreateFasSchemeResponse>, SaveFasSchemeDraftHandler>();
        services.AddScoped<ICommandHandler<ActivateFasSchemeDraftCommand, CreateFasSchemeResponse>, ActivateFasSchemeDraftHandler>();
        services.AddScoped<ICommandHandler<DeleteFasSchemeDraftCommand, bool>, DeleteFasSchemeDraftHandler>();
        services.AddScoped<ICommandHandler<PublishFasSchemeCommand, CreateFasSchemeResponse>, PublishFasSchemeHandler>();
        services.AddScoped<ICommandHandler<DisableFasSchemeCommand, CreateFasSchemeResponse>, DisableFasSchemeHandler>();
        services.AddScoped<ICommandHandler<DeleteFasSchemeCommand, CreateFasSchemeResponse>, DeleteFasSchemeHandler>();
        services.AddScoped<IQueryHandler<ListFasSchemesQuery, PageResponse<FasSchemeListItem>>, ListFasSchemesHandler>();
        services.AddScoped<IQueryHandler<GetFasSchemeQuery, FasSchemeDetail>, GetFasSchemeHandler>();
        services.AddScoped<IValidator<CreateFasSchemeRequest>, CreateFasSchemeRequestValidator>();
        services.AddScoped<IValidator<ListFasSchemesRequest>, ListFasSchemesRequestValidator>();
        services.AddScoped<IFasApplicationRepository, FasApplicationRepository>();
        services.AddScoped<StudentFasApplicationService>();
        services.AddScoped<FasEmailNotificationService>();
        services.AddScoped<FasInAppNotificationService>();
        services.AddScoped<FasApiExceptionFilter>();
        services.AddSingleton<IFasDocumentStorage>(sp => string.IsNullOrWhiteSpace(configuration["FasDocuments:AzureBlobConnectionString"])
            ? new PrivateFileFasDocumentStorage()
            : new AzureBlobFasDocumentStorage(configuration));
        services.AddSingleton<IFasDocumentScanner, ConfiguredFasDocumentScanner>();

        services.AddScoped<IQueryHandler<GetSchemeApplicationsQuery, GetSchemeApplicationsResponse>, GetSchemeApplicationsHandler>();
        services.AddScoped<IQueryHandler<GetApplicationDetailQuery, GetApplicationDetailResponse>, GetApplicationDetailHandler>();
        services.AddScoped<ICommandHandler<ApproveApplicationCommand, ApproveApplicationResponse>, ApproveApplicationHandler>();
        services.AddScoped<ICommandHandler<RejectApplicationCommand, RejectApplicationResponse>, RejectApplicationHandler>();

        services.AddOptions<StripePaymentOptions>().BindConfiguration(StripePaymentOptions.SectionName);
        services.AddScoped<IPaymentCheckoutRepository, PaymentCheckoutRepository>();
        services.AddScoped<IEnrollmentRefundPreviewRepository, EnrollmentRefundPreviewRepository>();
        services.AddScoped<IEnrollmentCancellationRepository, EnrollmentCancellationRepository>();
        services.AddScoped<IEnrollmentRefundProcessor, EnrollmentRefundProcessor>();
        services.AddScoped<IPaymentPersistenceTracker, PaymentPersistenceTracker>();
        services.AddScoped<IStripePaymentGateway, StripePaymentGateway>();
        services.AddSingleton<IStripeWebhookCoordinator, StripeWebhookCoordinator>();
        services.AddScoped<ICoursePaymentPlanGateway, CoursePaymentPlanGateway>();
        services.AddScoped<ICommandHandler<CreateCoursePaymentPlanCommand, CoursePaymentPlanResponse>, CreateCoursePaymentPlanHandler>();
        services.AddScoped<IQueryHandler<ListCoursePaymentPlansQuery, IReadOnlyCollection<CoursePaymentPlanResponse>>, ListCoursePaymentPlansHandler>();
        services.AddScoped<ICommandHandler<CreateStripeCheckoutCommand, StripeCheckoutResponse>, CreateStripeCheckoutHandler>();
        services.AddScoped<IQueryHandler<GetPaymentCheckoutStatusQuery, PaymentCheckoutStatusResponse>, GetPaymentCheckoutStatusHandler>();
        services.AddScoped<ICommandHandler<ProcessStripeWebhookCommand>, ProcessStripeWebhookHandler>();
        services.AddScoped<IQueryHandler<ListAdminPaymentsQuery, IReadOnlyCollection<AdminPaymentResponse>>, ListAdminPaymentsHandler>();
        services.AddScoped<IQueryHandler<ListPaymentWebhookEventsQuery, IReadOnlyCollection<PaymentWebhookEventResponse>>, ListPaymentWebhookEventsHandler>();
        services.AddScoped<ICommandHandler<CreatePaymentRefundCommand, PaymentRefundResponse>, CreatePaymentRefundHandler>();
        services.AddScoped<StatementPaymentPreviewBuilder>();
        services.AddScoped<IQueryHandler<PreviewStatementPaymentQuery, StatementPaymentPreviewResponse>, PreviewStatementPaymentHandler>();
        services.AddScoped<IQueryHandler<GetPendingEnrollmentPaymentQuery, PendingEnrollmentPaymentResponse?>, GetPendingEnrollmentPaymentHandler>();
        services.AddScoped<ICommandHandler<PayBillingStatementCommand, PayBillingStatementResponse>, PayBillingStatementHandler>();
        services.AddScoped<ICommandHandler<CancelBillingStatementPaymentCommand>, CancelBillingStatementPaymentHandler>();
        services.AddScoped<ICommandHandler<DeferBillingStatementCommand, DeferBillingStatementResponse>, DeferBillingStatementHandler>();
        services.AddScoped<IQueryHandler<ListUserPaymentHistoryQuery, PageResponse<UserPaymentHistoryResponse>>, ListUserPaymentHistoryHandler>();
        services.AddScoped<IQueryHandler<PreviewEnrollmentCancellationQuery, EnrollmentCancellationPreviewResponse>, PreviewEnrollmentCancellationHandler>();
        services.AddScoped<ICommandHandler<CancelEnrollmentCommand, EnrollmentCancellationResponse>, CancelEnrollmentHandler>();
        services.AddScoped<IValidator<CreateCoursePaymentPlanRequest>, CreateCoursePaymentPlanRequestValidator>();
        services.AddScoped<IValidator<CreateStripeCheckoutRequest>, CreateStripeCheckoutRequestValidator>();
        services.AddScoped<IValidator<CreatePaymentRefundRequest>, CreatePaymentRefundRequestValidator>();
        services.AddScoped<IValidator<PayBillingStatementRequest>, PayBillingStatementRequestValidator>();
        services.AddScoped<IValidator<CancelEnrollmentRequest>, CancelEnrollmentRequestValidator>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
