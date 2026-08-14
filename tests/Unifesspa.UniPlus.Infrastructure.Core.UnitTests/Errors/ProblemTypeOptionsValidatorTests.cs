namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Base malformada é defeito silencioso: o corpo de erro continua bem-formado e só quem
/// tentar seguir o link descobre. Por isso a recusa é no boot, e os casos cobertos aqui
/// são os que produziriam um <c>type</c> irresolvível pelo consumidor.
/// </summary>
public sealed class ProblemTypeOptionsValidatorTests
{
    private static readonly ProblemTypeOptionsValidator Validator = new();

    [Theory]
    [InlineData("https://unifesspa-edu-br.github.io/uniplus-developers/erros/")]
    [InlineData("https://unifesspa-edu-br.github.io/uniplus-developers/erros")]
    [InlineData("https://developers.uniplus.unifesspa.edu.br/erros/")]
    public void Validate_ComBaseAbsolutaHttps_Aceita(string baseUri)
    {
        ValidateOptionsResult resultado = Validar(baseUri);

        resultado.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_SemBase_Recusa(string baseUri)
    {
        ValidateOptionsResult resultado = Validar(baseUri);

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain("obrigatório");
    }

    [Fact]
    public void Validate_ComCaminhoRelativo_Recusa()
    {
        ValidateOptionsResult resultado = Validar("/erros/");

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain("absoluta");
    }

    [Fact]
    public void Validate_ComHttp_Recusa()
    {
        ValidateOptionsResult resultado = Validar("http://unifesspa-edu-br.github.io/uniplus-developers/erros/");

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain("HTTPS");
    }

    [Theory]
    [InlineData("https://unifesspa-edu-br.github.io/uniplus-developers/erros/?v=1")]
    [InlineData("https://unifesspa-edu-br.github.io/uniplus-developers/erros/#topo")]
    public void Validate_ComQueryOuFragmento_Recusa(string baseUri)
    {
        ValidateOptionsResult resultado = Validar(baseUri);

        resultado.Failed.Should().BeTrue(
            "o código é concatenado ao fim do caminho — query ou fragmento ficariam no meio da URI");
    }

    [Fact]
    public void Validate_DeNamedOptions_NaoSeAplica()
    {
        ValidateOptionsResult resultado = Validator.Validate(
            "outra", new ProblemTypeOptions { BaseUri = string.Empty });

        resultado.Skipped.Should().BeTrue();
    }

    private static ValidateOptionsResult Validar(string baseUri) =>
        Validator.Validate(Options.DefaultName, new ProblemTypeOptions { BaseUri = baseUri });
}
