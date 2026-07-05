using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.FasPayment;
using Moe.Modules.FasPayment.Infrastructure.Documents;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public class FasDocumentStorageRegistrationTests
{
    [Fact]
    public void AddServices_InvalidAzureBlobConnectionString_UsesPrivateFileStorage()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureBlob:ConnectionString"] = "UseDevelopmentStorage",
                ["AzureBlob:ContainerName"] = "fas-documents",
            })
            .Build();
        var services = new ServiceCollection();

        new FasPaymentModule().AddServices(services, configuration);

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IFasDocumentStorage>();

        storage.Should().BeOfType<PrivateFileFasDocumentStorage>();
    }
}
