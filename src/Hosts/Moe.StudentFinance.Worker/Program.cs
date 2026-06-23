using Moe.Application.Abstractions.Clock;
using Moe.Infrastructure.Shared.Clock;
using Moe.Modules.CourseBilling;
using Moe.Modules.EducationAccountTopUp;
using Moe.StudentFinance.Persistence;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddMoePersistence(builder.Configuration);
new EducationAccountTopUpModule().AddServices(builder.Services, builder.Configuration);
new CourseBillingModule().AddServices(builder.Services, builder.Configuration);
builder.Services.AddHostedService<Worker>();
await builder.Build().RunAsync();

public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("MOE worker heartbeat at {UtcNow}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
