namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Acompanhamento;

using AwesomeAssertions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.API;

public sealed class AcompanhamentoOptionsValidatorTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", false)]
    [InlineData("Staging", false)]
    [InlineData("Production", false)]
    [InlineData("Custom", false)]
    public void Validate_ValidacaoHabilitada_RestringeAmbientes(string environmentName, bool permitido)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        AcompanhamentoOptionsValidator validator = new(environment);

        ValidateOptionsResult result = validator.Validate(
            Options.DefaultName, new AcompanhamentoOptions { ValidacaoHabilitada = true });

        result.Succeeded.Should().Be(permitido);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Custom")]
    public void Validate_Desligada_AceitaQualquerAmbiente(string environmentName)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        AcompanhamentoOptionsValidator validator = new(environment);

        ValidateOptionsResult result = validator.Validate(
            Options.DefaultName, new AcompanhamentoOptions());

        result.Succeeded.Should().BeTrue();
    }
}
