using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Infrastructure.People;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure.People;

public sealed class PersonLifecycleGatewayTests
{
    [Fact]
    public async Task EnableAsync_OnDisabledPerson_SetsPersonStatusToActive()
    {
        DateTime enabledAtUtc = new(2026, 6, 30, 10, 0, 0, DateTimeKind.Utc);
        await using MoeDbContext dbContext = CreateDbContext();
        Person person = new(5001, "P-5001", "Student One", new DateOnly(2010, 1, 1), "SG", "CITIZEN");
        person.Disable(enabledAtUtc.AddMinutes(-5));
        dbContext.Add(person);
        await dbContext.SaveChangesAsync();
        PersonLifecycleGateway gateway = new(dbContext);

        var result = await gateway.EnableAsync(5001, enabledAtUtc, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        person.PersonStatusCode.Should().Be("ACTIVE");
        person.UpdatedAtUtc.Should().Be(enabledAtUtc);
    }

    private static MoeDbContext CreateDbContext()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"person-lifecycle-{Guid.NewGuid():N}")
            .Options;

        return new MoeDbContext(options, [new TestModelConfiguration()]);
    }

    private sealed class TestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
                builder.Ignore(x => x.DomainEvents);
            });
        }
    }
}
