using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Moe.Modules.IdentityPlatform.Infrastructure.Authentication;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure;

public sealed class EServiceLoginResolverTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 6, 30, 9, 0, 0, TimeSpan.Zero);
    private readonly MoeDbContext _dbContext;
    private readonly FakeEducationAccountProvisioningGateway _educationAccounts = new();
    private readonly EServiceLoginResolver _resolver;

    public EServiceLoginResolverTests()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"eservice-login-{Guid.NewGuid():N}")
            .Options;

        _dbContext = new MoeDbContext(options, [new EServiceLoginTestModelConfiguration()]);
        _resolver = new EServiceLoginResolver(_dbContext, _educationAccounts, new TestClock(Now));
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ResolveAsync_OnNewLogin_WithActiveEnrollmentAndNoEducationAccount_Succeeds()
    {
        AddPerson(5001, "subject-5001");
        AddActiveEnrollment(personId: 5001);
        await _dbContext.SaveChangesAsync();

        EServiceLoginResolution result = await _resolver.ResolveAsync(CreateLogin("subject-5001"), CancellationToken.None);

        result.PersonId.Should().Be(5001);
        _educationAccounts.CheckedActivePersonIds.Should().ContainSingle().Which.Should().Be(5001);
    }

    [Fact]
    public async Task ResolveAsync_OnNewLogin_WithActiveEnrollmentAndClosedEducationAccount_Succeeds()
    {
        AddPerson(5002, "subject-5002");
        AddActiveEnrollment(personId: 5002);
        _educationAccounts.AccountStatusByPersonId[5002] = "CLOSED";
        await _dbContext.SaveChangesAsync();

        EServiceLoginResolution result = await _resolver.ResolveAsync(CreateLogin("subject-5002"), CancellationToken.None);

        result.PersonId.Should().Be(5002);
    }

    [Fact]
    public async Task ResolveAsync_OnNewLogin_WithNoActiveEnrollmentAndActiveEducationAccount_Succeeds()
    {
        AddPerson(5003, "subject-5003");
        _educationAccounts.AccountStatusByPersonId[5003] = "ACTIVE";
        await _dbContext.SaveChangesAsync();

        EServiceLoginResolution result = await _resolver.ResolveAsync(CreateLogin("subject-5003"), CancellationToken.None);

        result.PersonId.Should().Be(5003);
    }

    [Fact]
    public async Task ResolveAsync_OnNewLogin_WithNoActiveEnrollmentAndClosedEducationAccount_Fails()
    {
        AddPerson(5004, "subject-5004");
        _educationAccounts.AccountStatusByPersonId[5004] = "CLOSED";
        await _dbContext.SaveChangesAsync();

        Func<Task> act = () => _resolver.ResolveAsync(CreateLogin("subject-5004"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("No eligible MOE profile was found for this Singpass user.");
    }

    [Fact]
    public async Task ResolveAsync_OnExistingAccount_WithNoActiveEnrollmentAndClosedEducationAccount_Fails()
    {
        AddPerson(5005, "subject-5005");
        _educationAccounts.AccountStatusByPersonId[5005] = "CLOSED";
        _dbContext.Add(UserAccount.CreateStudentSingpass(
            personId: 5005,
            externalIssuer: "mockpass",
            externalSubjectId: "subject-5005",
            displayName: "Student 5005",
            createdByUserAccountId: null,
            Now.UtcDateTime.AddDays(-1)));
        await _dbContext.SaveChangesAsync();

        Func<Task> act = () => _resolver.ResolveAsync(CreateLogin("subject-5005"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("No eligible MOE profile was found for this Singpass user.");
    }

    [Fact]
    public async Task ResolveAsync_OnExistingAccount_WithNoActiveEnrollmentAndActiveEducationAccount_Succeeds()
    {
        AddPerson(5006, "subject-5006");
        _educationAccounts.AccountStatusByPersonId[5006] = "ACTIVE";
        _dbContext.Add(UserAccount.CreateStudentSingpass(
            personId: 5006,
            externalIssuer: "mockpass",
            externalSubjectId: "subject-5006",
            displayName: "Student 5006",
            createdByUserAccountId: null,
            Now.UtcDateTime.AddDays(-1)));
        await _dbContext.SaveChangesAsync();

        EServiceLoginResolution result = await _resolver.ResolveAsync(CreateLogin("subject-5006"), CancellationToken.None);

        result.PersonId.Should().Be(5006);
    }

    private void AddPerson(long personId, string externalReference)
    {
        _dbContext.Add(new Person(
            personId,
            externalReference,
            $"Student {personId}",
            new DateOnly(2010, 1, 1),
            "SG",
            "CITIZEN"));
    }

    private void AddActiveEnrollment(long personId)
    {
        _dbContext.Add(new SchoolEnrollment(
            personId,
            organizationId: 10,
            studentNumber: $"S{personId}",
            academicYear: "2026",
            levelCode: "SEC1",
            classCode: "1A",
            startDate: DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-30),
            Now.UtcDateTime));
    }

    private static SingpassLoginResult CreateLogin(string subject)
        => new(
            ExternalIssuer: "mockpass",
            ExternalSubjectId: subject,
            IdentityNumber: $"T{subject[^4..]}A",
            DisplayName: $"Student {subject}",
            AuthenticationContext: "urn:mockpass:aal2",
            AuthenticationMethod: "pwd");

    private sealed class FakeEducationAccountProvisioningGateway : IEducationAccountProvisioningGateway
    {
        public Dictionary<long, string> AccountStatusByPersonId { get; } = [];
        public List<long> CheckedPersonIds { get; } = [];
        public List<long> CheckedActivePersonIds { get; } = [];

        public Task<EducationAccountProvisioningResult> EnsureAccountForStudentAsync(
            long personId,
            long openedByUserAccountId,
            DateTimeOffset openedAtUtc,
            CancellationToken cancellationToken,
            bool saveChanges = true)
            => throw new NotSupportedException();

        public Task<bool> HasAccountAsync(long personId, CancellationToken cancellationToken)
        {
            CheckedPersonIds.Add(personId);
            return Task.FromResult(AccountStatusByPersonId.ContainsKey(personId));
        }

        public Task<bool> HasActiveAccountAsync(long personId, CancellationToken cancellationToken)
        {
            CheckedActivePersonIds.Add(personId);
            return Task.FromResult(
                AccountStatusByPersonId.TryGetValue(personId, out string? statusCode)
                && statusCode == "ACTIVE");
        }
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class EServiceLoginTestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedNever();
                builder.Ignore(x => x.DomainEvents);
            });

            modelBuilder.Entity<SchoolEnrollment>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<UserAccount>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
                builder.Ignore(x => x.DomainEvents);
            });

            modelBuilder.Entity<UserAccessScope>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
                builder.Ignore(x => x.DomainEvents);
            });

            modelBuilder.Entity<PersonIdentifier>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
                builder.Ignore(x => x.DomainEvents);
            });
        }
    }
}
