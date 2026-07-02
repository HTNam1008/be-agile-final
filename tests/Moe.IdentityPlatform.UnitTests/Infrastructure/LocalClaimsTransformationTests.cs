using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Infrastructure.Authentication;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure;

public sealed class LocalClaimsTransformationTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TransformAsync_ForSingpassAccountWithDisabledPerson_DoesNotAddLocalClaims()
    {
        await using MoeDbContext dbContext = CreateDbContext();
        Person person = AddPerson(dbContext, 5001);
        person.Disable(Now.UtcDateTime);
        UserAccount account = UserAccount.CreateStudentSingpass(
            person.Id,
            "mockpass",
            "subject-5001",
            "Student 5001",
            createdByUserAccountId: null,
            Now.UtcDateTime);
        dbContext.Add(account);
        await dbContext.SaveChangesAsync();
        AddScope(dbContext, account.Id, RoleCodes.Student);
        await dbContext.SaveChangesAsync();
        LocalClaimsTransformation transformation = new(dbContext, new TestClock(Now));

        ClaimsPrincipal result = await transformation.TransformAsync(CreateSingpassPrincipal("mockpass", "subject-5001"));

        result.Identities.Should().NotContain(identity => identity.AuthenticationType == "MoeLocalIdentity");
        result.HasClaim(LocalIdentityClaimNames.Role, RoleCodes.Student).Should().BeFalse();
        result.HasClaim(LocalIdentityClaimNames.Portal, PortalCodes.EService).Should().BeFalse();
    }

    [Fact]
    public async Task TransformAsync_ForAdminAccountWithoutPerson_AddsLocalClaims()
    {
        await using MoeDbContext dbContext = CreateDbContext();
        UserAccount account = UserAccount.CreateAdmin(
            "https://login.microsoftonline.com/tenant/v2.0",
            "admin-subject",
            "tenant",
            "admin-object",
            "admin@moe.local",
            "Admin One",
            RoleCodes.HqAdmin,
            OrganizationUnitCodes.MoeHeadquartersId,
            createdByUserAccountId: 1,
            Now.UtcDateTime);
        dbContext.Add(account);
        await dbContext.SaveChangesAsync();
        AddScope(dbContext, account.Id, RoleCodes.HqAdmin);
        await dbContext.SaveChangesAsync();
        LocalClaimsTransformation transformation = new(dbContext, new TestClock(Now));

        ClaimsPrincipal result = await transformation.TransformAsync(CreateAdminPrincipal(
            "https://login.microsoftonline.com/tenant/v2.0",
            "admin-subject",
            "tenant",
            "admin-object"));

        result.Identities.Should().Contain(identity => identity.AuthenticationType == "MoeLocalIdentity");
        result.HasClaim(LocalIdentityClaimNames.Role, RoleCodes.HqAdmin).Should().BeTrue();
        result.HasClaim(LocalIdentityClaimNames.Portal, PortalCodes.Admin).Should().BeTrue();
        result.HasClaim(claim => claim.Type == LocalIdentityClaimNames.PersonId).Should().BeFalse();
    }

    [Fact]
    public async Task TransformAsync_ForSingpassAccountWithActivePerson_AddsLocalClaims()
    {
        await using MoeDbContext dbContext = CreateDbContext();
        Person person = AddPerson(dbContext, 5002);
        UserAccount account = UserAccount.CreateStudentSingpass(
            person.Id,
            "mockpass",
            "subject-5002",
            "Student 5002",
            createdByUserAccountId: null,
            Now.UtcDateTime);
        dbContext.Add(account);
        await dbContext.SaveChangesAsync();
        AddScope(dbContext, account.Id, RoleCodes.Student);
        await dbContext.SaveChangesAsync();
        LocalClaimsTransformation transformation = new(dbContext, new TestClock(Now));

        ClaimsPrincipal result = await transformation.TransformAsync(CreateSingpassPrincipal("mockpass", "subject-5002"));

        result.Identities.Should().Contain(identity => identity.AuthenticationType == "MoeLocalIdentity");
        result.HasClaim(LocalIdentityClaimNames.Role, RoleCodes.Student).Should().BeTrue();
        result.HasClaim(LocalIdentityClaimNames.Portal, PortalCodes.EService).Should().BeTrue();
        result.HasClaim(LocalIdentityClaimNames.PersonId, person.Id.ToString()).Should().BeTrue();
    }

    private static MoeDbContext CreateDbContext()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"local-claims-transformation-{Guid.NewGuid():N}")
            .Options;

        return new MoeDbContext(options, [new TestModelConfiguration()]);
    }

    private static Person AddPerson(MoeDbContext dbContext, long personId)
    {
        Person person = new(
            personId,
            $"subject-{personId}",
            $"Student {personId}",
            new DateOnly(2010, 1, 1),
            "SG",
            "CITIZEN");
        dbContext.Add(person);
        return person;
    }

    private static void AddScope(MoeDbContext dbContext, long userAccountId, string roleCode)
    {
        dbContext.Add(new UserAccessScope(
            userAccountId,
            OrganizationUnitCodes.MoeHeadquartersId,
            roleCode,
            createdByUserAccountId: userAccountId,
            Now.UtcDateTime.AddMinutes(-1),
            Now.UtcDateTime.AddMinutes(-1)));
    }

    private static ClaimsPrincipal CreateSingpassPrincipal(string issuer, string subject)
        => new(new ClaimsIdentity(
            [
                new Claim("iss", issuer),
                new Claim("sub", subject),
                new Claim(LocalIdentityClaimNames.ExternalAuthenticationScheme, AuthenticationSchemes.EServiceSingpass)
            ],
            AuthenticationSchemes.EServiceSingpass));

    private static ClaimsPrincipal CreateAdminPrincipal(
        string issuer,
        string subject,
        string tenantId,
        string objectId)
        => new(new ClaimsIdentity(
            [
                new Claim("iss", issuer),
                new Claim("sub", subject),
                new Claim("tid", tenantId),
                new Claim("oid", objectId),
                new Claim(LocalIdentityClaimNames.ExternalAuthenticationScheme, AuthenticationSchemes.AdminEntra)
            ],
            AuthenticationSchemes.AdminEntra));

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
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

            modelBuilder.Entity<RolePermission>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
            });
        }
    }
}
