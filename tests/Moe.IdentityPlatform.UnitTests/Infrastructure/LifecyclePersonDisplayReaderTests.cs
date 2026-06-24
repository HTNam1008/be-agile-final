using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.IdentityPlatform.Infrastructure.People;
using Moe.Modules.IdentityPlatform.Infrastructure.Persistence;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure;

public sealed class LifecyclePersonDisplayReaderTests
{
    [Fact]
    public async Task FindByPersonIdsAsync_Returns_Active_School_Name()
    {
        using MoeDbContext dbContext = CreateDbContext();
        Person person = new(
            9101,
            "TEST-9101",
            "Lifecycle Student",
            new DateOnly(2004, 6, 24),
            "SG",
            ResidencyStatusCodes.Citizen);
        OrganizationUnit school = CreateSchool(9901, "QA Lifecycle School");
        SchoolEnrollment enrollment = new(
            person.Id,
            school.Id,
            "STU-9101",
            "2026",
            "UNI_Y2",
            "QA-L1",
            new DateOnly(2026, 1, 1),
            new DateTime(2026, 6, 24, 2, 0, 0, DateTimeKind.Utc));
        dbContext.AddRange(person, school, enrollment);
        await dbContext.SaveChangesAsync();

        LifecyclePersonDisplayReader reader = new(dbContext);

        IReadOnlyCollection<LifecyclePersonDisplaySummary> result =
            await reader.FindByPersonIdsAsync([person.Id], CancellationToken.None);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new LifecyclePersonDisplaySummary(
                person.Id,
                "Lifecycle Student",
                string.Empty,
                "QA Lifecycle School"));
    }

    private static OrganizationUnit CreateSchool(long id, string schoolName)
    {
        OrganizationUnit school = new(
            $"QA-SCHOOL-{id}",
            schoolName,
            "SCHOOL",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        typeof(OrganizationUnit).GetProperty(nameof(OrganizationUnit.Id))!.SetValue(school, id);
        return school;
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
        {
            modelBuilder.ApplyConfiguration(new PersonConfiguration());
            modelBuilder.ApplyConfiguration(new OrganizationUnitConfiguration());
            modelBuilder.ApplyConfiguration(new SchoolEnrollmentConfiguration());
        }
    }
}
