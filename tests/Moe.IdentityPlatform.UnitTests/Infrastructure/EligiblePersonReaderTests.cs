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
    public async Task FindEligibleForEducationAccountAsync_ReturnsActiveSingaporeNationalsAged16Through30()
    {
        using MoeDbContext dbContext = CreateDbContext();
        dbContext.Set<Person>().Add(CreatePerson(1, Today.AddYears(-16), "SG", ResidencyStatusCodes.Citizen));
        dbContext.Set<Person>().Add(CreatePerson(2, Today.AddYears(-31).AddDays(1), "SG", ResidencyStatusCodes.Citizen));
        dbContext.Set<Person>().Add(CreatePerson(3, Today.AddYears(-16).AddDays(1), "SG", ResidencyStatusCodes.Citizen));
        dbContext.Set<Person>().Add(CreatePerson(4, Today.AddYears(-31), "SG", ResidencyStatusCodes.Citizen));
        dbContext.Set<Person>().Add(CreatePerson(5, Today.AddYears(-20), "MY", ResidencyStatusCodes.Citizen));
        Person disabled = CreatePerson(6, Today.AddYears(-20), "SG", ResidencyStatusCodes.Citizen);
        disabled.Disable(DateTime.UtcNow);
        dbContext.Set<Person>().Add(disabled);
        await dbContext.SaveChangesAsync();

        EligiblePersonReader reader = new(dbContext);

        IReadOnlyCollection<long> result =
            await reader.FindEligibleForEducationAccountAsync(Today, CancellationToken.None);

        result.Should().BeEquivalentTo([1L, 2L]);
    }

    [Fact]
    public async Task FindEligibleForEducationAccountAsync_UsesNationalityInsteadOfCitizenshipStatus()
    {
        using MoeDbContext dbContext = CreateDbContext();
        dbContext.Set<Person>().Add(CreatePerson(7, Today.AddYears(-20), "SG", ResidencyStatusCodes.PermanentResident));
        dbContext.Set<Person>().Add(CreatePerson(8, Today.AddYears(-20), "MY", ResidencyStatusCodes.Citizen));
        await dbContext.SaveChangesAsync();

        EligiblePersonReader reader = new(dbContext);

        IReadOnlyCollection<long> result =
            await reader.FindEligibleForEducationAccountAsync(Today, CancellationToken.None);

        result.Should().BeEquivalentTo([7L]);
    }

    [Fact]
    public async Task FindEligibleForEducationAccountAsync_NormalizesSingaporeNationalityVariants()
    {
        using MoeDbContext dbContext = CreateDbContext();
        dbContext.Set<Person>().Add(CreatePerson(11, Today.AddYears(-20), "Singapore", ResidencyStatusCodes.ValidPassHolder));
        dbContext.Set<Person>().Add(CreatePerson(12, Today.AddYears(-20), " singapore ", ResidencyStatusCodes.ValidPassHolder));
        dbContext.Set<Person>().Add(CreatePerson(13, Today.AddYears(-20), "sG", ResidencyStatusCodes.ValidPassHolder));
        await dbContext.SaveChangesAsync();

        EligiblePersonReader reader = new(dbContext);

        IReadOnlyCollection<long> result =
            await reader.FindEligibleForEducationAccountAsync(Today, CancellationToken.None);

        result.Should().BeEquivalentTo([11L, 12L, 13L]);
    }

    [Fact]
    public void FindEligibleForEducationAccountAsync_NationalityFilter_IsSqlTranslatable()
    {
        using MoeDbContext dbContext = CreateSqlServerDbContext();
        EligiblePersonReader reader = new(dbContext);

        string sql = reader.BuildEligibleForEducationAccountQuery(Today).ToQueryString();

        sql.Should().Contain("LTRIM(RTRIM");
        sql.Should().Contain("UPPER");
        sql.Should().Contain("SG");
        sql.Should().Contain("SINGAPORE");
    }

    [Fact]
    public async Task FindEligibleForEducationAccountAsync_DoesNotRequireSchoolEnrollment()
    {
        using MoeDbContext dbContext = CreateDbContext();
        dbContext.Set<Person>().Add(CreatePerson(10, Today.AddYears(-20), "SG", ResidencyStatusCodes.Citizen));
        await dbContext.SaveChangesAsync();

        EligiblePersonReader reader = new(dbContext);

        IReadOnlyCollection<long> result =
            await reader.FindEligibleForEducationAccountAsync(Today, CancellationToken.None);

        result.Should().ContainSingle().Which.Should().Be(10);
    }

    [Fact]
    public async Task FindPersonIdsAgedAtLeastAsync_FiltersSpecificPeopleByMinimumAge()
    {
        using MoeDbContext dbContext = CreateDbContext();
        dbContext.Set<Person>().Add(CreatePerson(20, Today.AddYears(-30), "SG", ResidencyStatusCodes.PermanentResident));
        dbContext.Set<Person>().Add(CreatePerson(21, Today.AddYears(-30).AddDays(1), "SG", ResidencyStatusCodes.Citizen));
        dbContext.Set<Person>().Add(CreatePerson(22, Today.AddYears(-30), "SG", ResidencyStatusCodes.Citizen));
        dbContext.Set<Person>().Add(CreatePerson(23, Today.AddYears(-31), "SG", ResidencyStatusCodes.Citizen));
        dbContext.Set<Person>().Add(CreatePerson(24, Today.AddYears(-40), "SG", ResidencyStatusCodes.Citizen));
        await dbContext.SaveChangesAsync();

        EligiblePersonReader reader = new(dbContext);

        IReadOnlyCollection<long> result = await reader.FindPersonIdsAgedAtLeastAsync(
            [20, 21, 22, 23],
            minAge: 30,
            Today,
            CancellationToken.None);

        result.Should().BeEquivalentTo([20L, 22L, 23L]);
    }

    private static Person CreatePerson(
        long id,
        DateOnly dateOfBirth,
        string nationalityCode,
        string citizenshipStatusCode)
    {
        return new Person(
            id,
            $"TEST-{id}",
            $"Student {id}",
            dateOfBirth,
            nationalityCode,
            citizenshipStatusCode);
    }

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MoeDbContext(options, [new TestModelConfigurationContributor()]);
    }

    private static MoeDbContext CreateSqlServerDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=TranslationOnly;Trusted_Connection=True;")
            .Options;

        return new MoeDbContext(options, [new TestModelConfigurationContributor()]);
    }

    private sealed class TestModelConfigurationContributor : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
            => modelBuilder.ApplyConfiguration(new PersonConfiguration());
    }
}
