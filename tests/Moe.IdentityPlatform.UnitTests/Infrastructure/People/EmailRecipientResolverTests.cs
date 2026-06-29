using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moe.Modules.IdentityPlatform;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.IdentityPlatform.Infrastructure.People;
using Moe.Modules.MailDelivery.Infrastructure.Smtp;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure.People;

public sealed class EmailRecipientResolverTests
{
    [Fact]
    public async Task ResolveForPersonAsync_PrefersPreferredThenContactThenOfficial()
    {
        await using MoeDbContext db = CreateDbContext();
        Person person = CreatePerson("official@example.com");
        person.UpdatePreferredContact("preferred@example.com", null, null, DateTime.UtcNow);
        UserAccount account = UserAccount.CreateStudentSingpass(
            person.Id,
            "issuer",
            "subject",
            "Student",
            null,
            DateTime.UtcNow);
        account.UpdateContactDetails("contact@example.com", null, DateTime.UtcNow);
        db.AddRange(person, account);
        await db.SaveChangesAsync();

        EmailRecipient? result = await CreateResolver(db, Environments.Production)
            .ResolveForPersonAsync(person.Id, CancellationToken.None);

        result.Should().Be(new EmailRecipient("preferred@example.com", EmailRecipientSourceCodes.Preferred));
    }

    [Fact]
    public async Task ResolveForPersonAsync_UsesContactWhenPreferredIsInvalid()
    {
        await using MoeDbContext db = CreateDbContext();
        Person person = CreatePerson("official@example.com");
        person.UpdatePreferredContact("invalid-address", null, null, DateTime.UtcNow);
        UserAccount account = UserAccount.CreateStudentSingpass(
            person.Id,
            "issuer",
            "subject",
            "Student",
            null,
            DateTime.UtcNow);
        account.UpdateContactDetails("contact@example.com", null, DateTime.UtcNow);
        db.AddRange(person, account);
        await db.SaveChangesAsync();

        EmailRecipient? result = await CreateResolver(db, Environments.Production)
            .ResolveForPersonAsync(person.Id, CancellationToken.None);

        result.Should().Be(new EmailRecipient("contact@example.com", EmailRecipientSourceCodes.Contact));
    }

    [Fact]
    public async Task ResolveForPersonAsync_UsesOfficialWhenOtherCandidatesAreMissing()
    {
        await using MoeDbContext db = CreateDbContext();
        Person person = CreatePerson("official@example.com");
        person.UpdatePreferredContact(null, null, null, DateTime.UtcNow);
        db.Add(person);
        await db.SaveChangesAsync();

        EmailRecipient? result = await CreateResolver(db, Environments.Production)
            .ResolveForPersonAsync(person.Id, CancellationToken.None);

        result.Should().Be(new EmailRecipient("official@example.com", EmailRecipientSourceCodes.Official));
    }

    [Fact]
    public async Task ResolveForPersonAsync_UsesConfiguredFallbackOnlyInDevelopment()
    {
        await using MoeDbContext db = CreateDbContext();

        EmailRecipient? development = await CreateResolver(db, Environments.Development)
            .ResolveForPersonAsync(999, CancellationToken.None);
        EmailRecipient? production = await CreateResolver(db, Environments.Production)
            .ResolveForPersonAsync(999, CancellationToken.None);

        development.Should().Be(new EmailRecipient(
            "fallback@example.com",
            EmailRecipientSourceCodes.DevelopmentFallback));
        production.Should().BeNull();
    }

    [Fact]
    public void ResolveProvided_UsesApplicationEmailBeforeDevelopmentFallback()
    {
        using MoeDbContext db = CreateDbContext();
        EmailRecipientResolver resolver = CreateResolver(db, Environments.Development);

        resolver.ResolveProvided("fas-applicant@example.com").Should().Be(
            new EmailRecipient("fas-applicant@example.com", EmailRecipientSourceCodes.Provided));
        resolver.ResolveProvided("invalid-address").Should().Be(
            new EmailRecipient("fallback@example.com", EmailRecipientSourceCodes.DevelopmentFallback));
    }

    private static MoeDbContext CreateDbContext()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"email-recipient-{Guid.NewGuid():N}")
            .Options;
        return new MoeDbContext(options, [new IdentityPlatformModelConfiguration()]);
    }

    private static Person CreatePerson(string officialEmail)
    {
        Person person = Person.CreateStudent(
            $"EXT-{Guid.NewGuid():N}",
            "S****123A",
            "Test Student",
            new DateOnly(2000, 1, 1),
            "SG",
            "CITIZEN",
            officialEmail,
            null,
            null,
            DateTime.UtcNow);
        typeof(Person).GetProperty(nameof(Person.Id))!.SetValue(person, Random.Shared.Next(1, int.MaxValue));
        return person;
    }

    private static EmailRecipientResolver CreateResolver(MoeDbContext db, string environmentName)
        => new(
            db,
            new TestHostEnvironment(environmentName),
            Options.Create(new MailDeliveryOptions
            {
                DevelopmentFallbackRecipient = "fallback@example.com"
            }));

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
