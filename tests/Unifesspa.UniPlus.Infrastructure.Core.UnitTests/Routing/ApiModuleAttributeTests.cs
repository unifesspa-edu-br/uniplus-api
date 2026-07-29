namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Routing;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Routing;

public sealed class ApiModuleAttributeTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Portal")]
    [InlineData("1portal")]
    [InlineData("portal/api")]
    [InlineData("portal_api")]
    public void Constructor_NameInvalid_ThrowsArgumentException(string name)
    {
        Action act = () => _ = new ApiModuleAttribute(name);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(name));
    }

    [Fact]
    public void Constructor_NameValid_PreservesName()
    {
        var attribute = new ApiModuleAttribute("portal-candidato");

        attribute.Name.Should().Be("portal-candidato");
    }

    [Fact]
    public void GetRequiredName_AssemblyWithoutMetadata_ThrowsInvalidOperationException()
    {
        Action act = () => ApiModuleMetadata.GetRequiredName(
            typeof(ApiModuleAttributeTests).Assembly);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*não declara*ApiModuleAttribute*");
    }
}
