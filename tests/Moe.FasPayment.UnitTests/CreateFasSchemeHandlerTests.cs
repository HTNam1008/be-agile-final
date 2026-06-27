using FluentAssertions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.IGateway.Courses;
using Moe.Modules.FasPayment.Application.AdminFasSchemes;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moq;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class CreateFasSchemeHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 22, 10, 30, 0, TimeSpan.Zero);
    private readonly Mock<IFasSchemeRepository> _repository = new();
    private readonly Mock<ICourseReferenceDirectory> _courses = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IClock> _clock = new();

    public CreateFasSchemeHandlerTests()
    {
        _currentUser.SetupGet(x => x.UserAccountId).Returns(77);
        _clock.SetupGet(x => x.UtcNow).Returns(Now);
        _courses.Setup(x => x.FindUnknownCourseIdsAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(x => x.CreateAsync(It.IsAny<CreateFasSchemeRequest>(), It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateFasSchemeResponse(1, "MOE-FAS-2026", "GRANT-MOE-FAS-2026", "ACTIVE"));
    }

    [Fact]
    public async Task Passes_authenticated_actor_clock_and_courses_to_dependencies()
    {
        CreateFasSchemeRequest request = FasSchemeTestData.ValidRequest() with { CourseIds = [5, 7] };

        var result = await Handler().Handle(new(request), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _courses.Verify(x => x.FindUnknownCourseIdsAsync(It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 5, 7 })), It.IsAny<CancellationToken>()));
        _repository.Verify(x => x.GrantCodeExistsAsync("GRANT-MOE-FAS-2026", It.IsAny<CancellationToken>()));
        _repository.Verify(x => x.CreateAsync(
            It.Is<CreateFasSchemeRequest>(payload => payload.SchemeCode == request.SchemeCode && payload.GrantCode == "GRANT-MOE-FAS-2026"),
            77,
            Now.UtcDateTime,
            It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData(true, false, "FAS.DUPLICATE_SCHEME_CODE")]
    [InlineData(false, true, "FAS.DUPLICATE_GRANT_CODE")]
    public async Task Preexisting_codes_return_stable_errors(bool schemeExists, bool grantExists, string errorCode)
    {
        _repository.Setup(x => x.SchemeCodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(schemeExists);
        _repository.Setup(x => x.GrantCodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(grantExists);

        var result = await Handler().Handle(new(FasSchemeTestData.ValidRequest()), CancellationToken.None);

        result.Error.Code.Should().Be(errorCode);
        _repository.Verify(x => x.CreateAsync(It.IsAny<CreateFasSchemeRequest>(), It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reports_all_unknown_courses_without_writing()
    {
        _courses.Setup(x => x.FindUnknownCourseIdsAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>())).ReturnsAsync([4, 9]);

        var result = await Handler().Handle(new(FasSchemeTestData.ValidRequest() with { CourseIds = [4, 9] }), CancellationToken.None);

        result.Error.Code.Should().Be("FAS.UNKNOWN_COURSES");
        result.Error.Message.Should().Contain("4").And.Contain("9");
    }

    [Fact]
    public async Task Concurrent_unique_violation_is_translated_to_stable_error()
    {
        _repository.Setup(x => x.CreateAsync(It.IsAny<CreateFasSchemeRequest>(), It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FasSchemeWriteConflictException(FasSchemeUniqueField.GrantCode, new InvalidOperationException()));

        var result = await Handler().Handle(new(FasSchemeTestData.ValidRequest()), CancellationToken.None);

        result.Error.Code.Should().Be("FAS.DUPLICATE_GRANT_CODE");
    }

    private CreateFasSchemeHandler Handler() => new(_repository.Object, _courses.Object, _currentUser.Object, _clock.Object);
}
