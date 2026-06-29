using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.MailDelivery.Domain;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Infrastructure.Notifications;
using Moe.Modules.MailDelivery.Infrastructure.Smtp;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.MailDelivery.UnitTests;

public sealed class CreateStudentMailNotificationMiddlewareTests
{
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 6, 29, 3, 47, 13, TimeSpan.Zero));

    [Fact]
    public async Task InvokeAsync_OnSuccessfulCreateStudent_SendsMailAndPreservesResponse()
    {
        const string responseBody = """
            {"success":true,"code":201,"message":"Student created.","data":{"personId":912252,"schoolName":"National University of Singapore","studentNumber":"G33423","displayName":"1412341234"},"errors":null,"traceId":"trace-123","timestampUtc":"2026-06-29T03:47:13.6870746Z"}
            """;
        RecordingEmailDeliveryGateway gateway = new();
        CreateStudentMailNotificationMiddleware middleware = CreateMiddleware(
            gateway,
            WriteResponse(StatusCodes.Status201Created, responseBody));
        DefaultHttpContext context = CreateObservedContext();

        string actualBody = await InvokeAndReadResponseAsync(middleware, context);

        actualBody.Should().Be(responseBody);
        context.Response.StatusCode.Should().Be(StatusCodes.Status201Created);
        gateway.Messages.Should().ContainSingle();
        EmailDeliveryMessage message = gateway.Messages.Single();
        message.ToEmail.Should().Be("tphatdn1@gmail.com");
        message.Subject.Should().Be("MOE SEEDS - Create Student succeeded");
        message.PlainTextBody.Should().Contain("Status code: 201");
        message.PlainTextBody.Should().Contain("Trace ID: trace-123");
        message.PlainTextBody.Should().Contain("personId: 912252");
        message.PlainTextBody.Should().Contain("schoolName: National University of Singapore");
        message.PlainTextBody.Should().NotContain("IdentityNumber");
    }

    [Fact]
    public async Task InvokeAsync_OnBusinessError_SendsFailedMailAndPreservesResponse()
    {
        const string responseBody = """
            {"success":false,"code":409,"message":"The school identifiers do not match.","data":null,"errors":["IDENTITY.SCHOOL_IDENTIFIERS_CONFLICT"],"traceId":"trace-456"}
            """;
        RecordingEmailDeliveryGateway gateway = new();
        CreateStudentMailNotificationMiddleware middleware = CreateMiddleware(
            gateway,
            WriteResponse(StatusCodes.Status409Conflict, responseBody));
        DefaultHttpContext context = CreateObservedContext();

        string actualBody = await InvokeAndReadResponseAsync(middleware, context);

        actualBody.Should().Be(responseBody);
        gateway.Messages.Should().ContainSingle();
        EmailDeliveryMessage message = gateway.Messages.Single();
        message.Subject.Should().Be("MOE SEEDS - Create Student failed");
        message.PlainTextBody.Should().Contain("Errors: IDENTITY.SCHOOL_IDENTIFIERS_CONFLICT");
    }

    [Fact]
    public async Task InvokeAsync_OnValidationError_SendsFailedMailAndPreservesResponse()
    {
        const string responseBody = """
            {"success":false,"code":400,"message":"Validation failed.","data":null,"errors":["Email is not a valid email address."],"traceId":"trace-789"}
            """;
        RecordingEmailDeliveryGateway gateway = new();
        CreateStudentMailNotificationMiddleware middleware = CreateMiddleware(
            gateway,
            WriteResponse(StatusCodes.Status400BadRequest, responseBody));
        DefaultHttpContext context = CreateObservedContext();

        string actualBody = await InvokeAndReadResponseAsync(middleware, context);

        actualBody.Should().Be(responseBody);
        gateway.Messages.Should().ContainSingle();
        gateway.Messages.Single().PlainTextBody.Should().Contain("Validation failed.");
        gateway.Messages.Single().PlainTextBody.Should().Contain("Email is not a valid email address.");
    }

    [Fact]
    public async Task InvokeAsync_OnAuthStyleEmptyResponse_SendsFailedMailAndPreservesEmptyBody()
    {
        RecordingEmailDeliveryGateway gateway = new();
        CreateStudentMailNotificationMiddleware middleware = CreateMiddleware(
            gateway,
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            });
        DefaultHttpContext context = CreateObservedContext();

        string actualBody = await InvokeAndReadResponseAsync(middleware, context);

        actualBody.Should().BeEmpty();
        gateway.Messages.Should().ContainSingle();
        EmailDeliveryMessage message = gateway.Messages.Single();
        message.Subject.Should().Be("MOE SEEDS - Create Student failed");
        message.PlainTextBody.Should().Contain("Status code: 401");
        message.PlainTextBody.Should().Contain("Response body was empty.");
    }

    [Fact]
    public async Task InvokeAsync_OnNonMatchingRoute_DoesNotSendMail()
    {
        const string responseBody = "{\"success\":true}";
        RecordingEmailDeliveryGateway gateway = new();
        CreateStudentMailNotificationMiddleware middleware = CreateMiddleware(
            gateway,
            WriteResponse(StatusCodes.Status200OK, responseBody));
        DefaultHttpContext context = CreateContext(HttpMethods.Post, "/api/admin/v1/students/1");

        string actualBody = await InvokeAndReadResponseAsync(middleware, context);

        actualBody.Should().Be(responseBody);
        gateway.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenMailGatewayFails_PreservesOriginalResponse()
    {
        const string responseBody = """
            {"success":true,"code":201,"message":"Student created.","data":{"personId":912252},"errors":null,"traceId":"trace-999"}
            """;
        RecordingEmailDeliveryGateway gateway = new()
        {
            Result = Result.Failure(MailDeliveryErrors.MissingSmtpPassword)
        };
        CreateStudentMailNotificationMiddleware middleware = CreateMiddleware(
            gateway,
            WriteResponse(StatusCodes.Status201Created, responseBody));
        DefaultHttpContext context = CreateObservedContext();

        string actualBody = await InvokeAndReadResponseAsync(middleware, context);

        actualBody.Should().Be(responseBody);
        context.Response.StatusCode.Should().Be(StatusCodes.Status201Created);
        gateway.Messages.Should().ContainSingle();
    }

    private CreateStudentMailNotificationMiddleware CreateMiddleware(
        IEmailDeliveryGateway gateway,
        RequestDelegate next)
        => new(
            next,
            gateway,
            Options.Create(new MailDeliveryOptions
            {
                AppName = "MOE SEEDS",
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                UserName = "recthongminh1@gmail.com",
                FromEmail = "recthongminh1@gmail.com",
                FromDisplayName = "MOE SEEDS"
            }),
            _clock,
            NullLogger<CreateStudentMailNotificationMiddleware>.Instance);

    private static DefaultHttpContext CreateObservedContext()
        => CreateContext(HttpMethods.Post, "/api/admin/v1/students");

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        DefaultHttpContext context = new();
        context.Request.Method = method;
        context.Request.Path = path;
        context.TraceIdentifier = "fallback-trace";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static RequestDelegate WriteResponse(int statusCode, string responseBody)
        => async context =>
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(responseBody, Encoding.UTF8);
        };

    private static async Task<string> InvokeAndReadResponseAsync(
        CreateStudentMailNotificationMiddleware middleware,
        DefaultHttpContext context)
    {
        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        using StreamReader reader = new(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed class RecordingEmailDeliveryGateway : IEmailDeliveryGateway
    {
        public List<EmailDeliveryMessage> Messages { get; } = [];
        public Result Result { get; init; } = Result.Success();

        public Task<Result> SendAsync(EmailDeliveryMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(Result);
        }
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
