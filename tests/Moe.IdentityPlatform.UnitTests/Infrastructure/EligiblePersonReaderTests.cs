using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Infrastructure.People;
using Moe.Modules.IdentityPlatform.Infrastructure.Persistence;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure;

public sealed class EligiblePersonReaderTests
{
    private static readonly DateOnly Today = new(2026, 6, 24);

    [Fact]
    public async Task FindEligibleForEducationAccountAsync_ReturnsActiveCitizensAged16Through30()
    {
        using MoeDbContext dbContext = CreateDbContext();
        dbContext.Set<Person>().Add(CreatePerson(1, Today.AddYears(-16), ResidencyStatusCodes.Citizen));
        dbContext.Set<Person>().Add(CreatePerson(2, Today.AddYears(-31).AddDays(1), ResidencyStatusCodes.Citizen));
        dbContext.Set<Person>().Add(CreatePerson(3, Today.AddYears(-16).AddDays(1), ResidencyStatusCodes.Citizen));
        dbContext.Set<Person>().Add(CreatePerson(4, Today.AddYears(-31), ResidencyStatusCodes.Citizen));
        dbContext.Set<Person>().Add(CreatePerson(5, Today.AddYears(-20), ResidencyStatusCodes.PermanentResident));
        Person disabled = CreatePerson(6, Today.AddYears(-20), ResidencyStatusCodes.Citizen);
        disabled.Disable(DateTime.UtcNow);
        dbContext.Set<Person>().Add(disabled);
        await dbContext.SaveChangesAsync();

        EligiblePersonReader reader = new(dbContext);

        IReadOnlyCollection<long> result =
            await reader.FindEligibleForEducationAccountAsync(Today, CancellationToken.None);

        result.Should().BeEquivalentTo([1L, 2L]);
    }

    [Fact]
    public async Task FindEligibleForEducationAccountAsync_DoesNotRequireSchoolEnrollment()
    {
        using MoeDbContext dbContext = CreateDbContext();
        dbContext.Set<Person>().Add(CreatePerson(10, Today.AddYears(-20), ResidencyStatusCodes.Citizen));
        await dbContext.SaveChangesAsync();

        EligiblePersonReader reader = new(dbContext);

        IReadOnlyCollection<long> result =
            await reader.FindEligibleForEducationAccountAsync(Today, CancellationToken.None);

        result.Should().ContainSingle().Which.Should().Be(10);
    }

    private static Person CreatePerson(long id, DateOnly dateOfBirth, string citizenshipStatusCode)
    {
        return new Person(
            id,
            $"TEST-{id}",
            $"Student {id}",
            dateOfBirth,
            "SG",
            citizenshipStatusCode);
    }

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MoeDbContext(options, [new TestModelConfigurationContributor()]);
    }

    private sealed class TestModelConfigurationContributor : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
            => modelBuilder.ApplyConfiguration(new PersonConfiguration());
    }
}
