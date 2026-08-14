namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// A fábrica é o único ponto que monta o campo <c>type</c>. O que se cobre aqui é o
/// contrato que os quatro emissores de <c>problem+json</c> herdam dela: exatamente uma
/// barra entre base e código, seja qual for a pontuação com que a base foi configurada.
/// </summary>
public sealed class ProblemTypeUriFactoryTests
{
    private const string BaseUriDoCatalogo = "https://unifesspa-edu-br.github.io/uniplus-developers/erros/";

    [Theory]
    [InlineData("https://unifesspa-edu-br.github.io/uniplus-developers/erros/")]
    [InlineData("https://unifesspa-edu-br.github.io/uniplus-developers/erros")]
    // Barra duplicada é erro de digitação que a validação de boot aceita — a URI
    // resultante não resolveria no catálogo, então a normalização a absorve.
    [InlineData("https://unifesspa-edu-br.github.io/uniplus-developers/erros//")]
    [InlineData("https://unifesspa-edu-br.github.io/uniplus-developers/erros///")]
    public void Build_ComOuSemBarraFinalNaConfiguracao_ProduzAMesmaUri(string baseConfigurada)
    {
        ProblemTypeUriFactory fabrica = Criar(baseConfigurada);

        string type = fabrica.Build("uniplus.selecao.edital.nao_encontrado");

        type.Should().Be(BaseUriDoCatalogo + "uniplus.selecao.edital.nao_encontrado");
    }

    [Fact]
    public void Build_ComEspacoEmVoltaDaBase_IgnoraOEspaco()
    {
        ProblemTypeUriFactory fabrica = Criar($"  {BaseUriDoCatalogo}  ");

        string type = fabrica.Build("uniplus.validacao");

        type.Should().Be(BaseUriDoCatalogo + "uniplus.validacao");
    }

    [Fact]
    public void Construtor_ComBaseVazia_Lanca()
    {
        Action acao = () => Criar(string.Empty);

        acao.Should().Throw<ArgumentException>(
            "base ausente não pode virar type relativo silencioso — a falha pertence ao boot");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_SemCodigo_Lanca(string code)
    {
        ProblemTypeUriFactory fabrica = Criar(BaseUriDoCatalogo);

        Action acao = () => fabrica.Build(code);

        acao.Should().Throw<ArgumentException>();
    }

    private static ProblemTypeUriFactory Criar(string baseUri) =>
        new(Options.Create(new ProblemTypeOptions { BaseUri = baseUri }));
}
