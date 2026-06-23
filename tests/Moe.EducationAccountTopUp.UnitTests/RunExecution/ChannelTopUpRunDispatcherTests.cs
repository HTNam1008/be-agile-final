using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Modules.EducationAccountTopUp;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.EducationAccountTopUp.Infrastructure.TopUpRunDispatcher;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class ChannelTopUpRunDispatcherTests
{
    [Fact]
    public async Task Should_Enqueue_And_Read_RunId()
    {
        ChannelTopUpRunDispatcher dispatcher = new(NullLogger<ChannelTopUpRunDispatcher>.Instance);

        await dispatcher.EnqueueAsync(42);
        long runId = await dispatcher.Reader.ReadAsync();

        runId.Should().Be(42);
    }

    [Fact]
    public async Task Should_Enqueue_Multiple_RunIds_In_Order()
    {
        ChannelTopUpRunDispatcher dispatcher = new(NullLogger<ChannelTopUpRunDispatcher>.Instance);

        await dispatcher.EnqueueAsync(1);
        await dispatcher.EnqueueAsync(2);
        await dispatcher.EnqueueAsync(3);

        long[] runIds =
        [
            await dispatcher.Reader.ReadAsync(),
            await dispatcher.Reader.ReadAsync(),
            await dispatcher.Reader.ReadAsync()
        ];

        runIds.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Module_Should_Register_One_Shared_Channel_For_Dispatch_And_Read()
    {
        ServiceCollection services = new();
        services.AddLogging();

        new EducationAccountTopUpModule().AddServices(
            services,
            new ConfigurationBuilder().Build());

        services.Count(x => x.ServiceType == typeof(ITopUpRunDispatcher))
            .Should().Be(1);
        services.Count(x => x.ServiceType == typeof(ITopUpRunQueueReader))
            .Should().Be(1);
        services.Count(x => x.ServiceType == typeof(ITopUpCampaignRepository))
            .Should().Be(1);
        services.Count(x => x.ServiceType == typeof(ITopUpRunRepository))
            .Should().Be(1);
        services.Count(x => x.ServiceType == typeof(ITopUpTransactionRepository))
            .Should().Be(1);

        using ServiceProvider provider = services.BuildServiceProvider();
        ITopUpRunDispatcher dispatcher =
            provider.GetRequiredService<ITopUpRunDispatcher>();
        ITopUpRunQueueReader queueReader =
            provider.GetRequiredService<ITopUpRunQueueReader>();

        dispatcher.Should().BeSameAs(queueReader);
        dispatcher.Should().BeOfType<ChannelTopUpRunDispatcher>();
    }
}
