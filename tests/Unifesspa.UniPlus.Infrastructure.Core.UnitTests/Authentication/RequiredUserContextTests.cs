namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authentication;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Authentication;

public sealed class RequiredUserContextTests
{
    [Fact]
    public void UserId_Should_ReturnInner_WhenAuthenticated()
    {
        IUserContext inner = Substitute.For<IUserContext>();
        inner.IsAuthenticated.Returns(true);
        inner.UserId.Returns("user-42");
        RequiredUserContext sut = new(inner);

        sut.UserId.Should().Be("user-42");
    }

    [Fact]
    public void UserId_Should_Throw_WhenAnonymous()
    {
        IUserContext inner = Substitute.For<IUserContext>();
        inner.IsAuthenticated.Returns(false);
        RequiredUserContext sut = new(inner);

        FluentActions.Invoking(() => _ = sut.UserId)
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*acessado em request anônima*");
    }

    [Fact]
    public void UserId_Should_Throw_WhenAuthenticatedButClaimMissing()
    {
        IUserContext inner = Substitute.For<IUserContext>();
        inner.IsAuthenticated.Returns(true);
        inner.UserId.Returns((string?)null);
        RequiredUserContext sut = new(inner);

        FluentActions.Invoking(() => _ = sut.UserId)
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Claim 'UserId' ausente*");
    }

    [Fact]
    public void Roles_Should_ReturnInner_WhenAuthenticated()
    {
        IUserContext inner = Substitute.For<IUserContext>();
        inner.IsAuthenticated.Returns(true);
        inner.Roles.Returns(new[] { "admin", "gestor" });
        RequiredUserContext sut = new(inner);

        sut.Roles.Should().BeEquivalentTo(["admin", "gestor"]);
    }

    [Fact]
    public void Roles_Should_Throw_WhenAnonymous()
    {
        IUserContext inner = Substitute.For<IUserContext>();
        inner.IsAuthenticated.Returns(false);
        RequiredUserContext sut = new(inner);

        FluentActions.Invoking(() => _ = sut.Roles)
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void HasRole_Should_DelegateToInner_WhenAuthenticated()
    {
        IUserContext inner = Substitute.For<IUserContext>();
        inner.IsAuthenticated.Returns(true);
        inner.HasRole("admin").Returns(true);
        RequiredUserContext sut = new(inner);

        sut.HasRole("admin").Should().BeTrue();
    }

    [Fact]
    public void HasRole_Should_Throw_WhenAnonymous()
    {
        IUserContext inner = Substitute.For<IUserContext>();
        inner.IsAuthenticated.Returns(false);
        RequiredUserContext sut = new(inner);

        FluentActions.Invoking(() => sut.HasRole("admin"))
            .Should().Throw<InvalidOperationException>();
    }
}
