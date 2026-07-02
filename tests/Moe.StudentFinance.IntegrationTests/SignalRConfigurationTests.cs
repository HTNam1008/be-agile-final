using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class SignalRConfigurationTests
{
    [Fact]
    public void Startup_WhenAzureSignalRIsSelectedWithoutConnectionString_ThrowsClearException()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SignalR:Provider"] = "Azure",
                ["SignalR:AzureConnectionString"] = "",
                ["Azure:SignalR:ConnectionString"] = ""
            })
            .Build();
        ServiceCollection services = new();

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => ConfigureSignalR(services, configuration));

        Assert.Contains(
            "SignalR Azure provider requires SignalR:AzureConnectionString or Azure:SignalR:ConnectionString.",
            exception.InnerException?.Message);
    }

    private static void ConfigureSignalR(IServiceCollection services, IConfiguration configuration)
    {
        MethodInfo method = typeof(Program).GetMethod(
            "ConfigureSignalR",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        method.Invoke(null, [services, configuration]);
    }
}
