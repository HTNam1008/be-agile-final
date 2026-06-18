using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
}
