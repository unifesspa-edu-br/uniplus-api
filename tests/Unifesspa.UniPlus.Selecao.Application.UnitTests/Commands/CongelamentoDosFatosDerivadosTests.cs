namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A publicação congela o fato derivado do catálogo, e a retificação mantém o que a versão vigente
/// já congelou (ADR-0154).
/// </summary>
public sealed class CongelamentoDosFatosDerivadosTests
{
    private static FatoCandidatoView Derivado(string codigo, string classificacao) =>
        CatalogoDoConjuntoBasico.Fatos[0] with
        {
            Codigo = codigo,
            Nome = $"Nome de {codigo}",
            Origem = "DERIVADO",
            Dominio = "CATEGORICO",
            Cardinalidade = "MULTIVALORADO",
            ClassificacaoProtecao = classificacao,
            HipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL",
            FinalidadeTratamento = "Finalidade do catálogo.",
        };

    private static FatoCandidatoView Declarado(string codigo) =>
        CatalogoDoConjuntoBasico.Fatos[0] with { Codigo = codigo, Origem = "DECLARADO" };

    [Fact(DisplayName = "A publicação congela só os fatos derivados do catálogo, com o nome, o domínio, a cardinalidade e a proteção")]
    public void Resolver_CongelaOsDerivadosDoCatalogo()
    {
        IReadOnlyDictionary<string, FatoDerivadoCongelado> congelados = CongelamentoDosFatosDerivados.Resolver(
            [Derivado("MODALIDADE", "INTERNO"), Declarado("COR_RACA")], null);

        congelados.Keys.Should().Equal("MODALIDADE");
        congelados["MODALIDADE"].Should().Be(new FatoDerivadoCongelado(
            "MODALIDADE", "Nome de MODALIDADE", "CATEGORICO", "MULTIVALORADO", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Finalidade do catálogo."));
    }

    [Fact(DisplayName = "A retificação mantém o derivado que a versão vigente congelou, e só o derivado novo vem do catálogo de hoje")]
    public void Resolver_VersaoVigenteMantemOCongelado()
    {
        Dictionary<string, FatoDerivadoCongelado> daVersao = new Dictionary<string, FatoDerivadoCongelado>(StringComparer.Ordinal)
        {
            ["MODALIDADE"] = new("MODALIDADE", "Modalidade congelada", "CATEGORICO", "MULTIVALORADO", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Finalidade congelada."),
        };

        IReadOnlyDictionary<string, FatoDerivadoCongelado> congelados = CongelamentoDosFatosDerivados.Resolver(
            [Derivado("MODALIDADE", "SENSIVEL"), Derivado("FAIXA_ETARIA", "PESSOAL")], daVersao);

        congelados["MODALIDADE"].Should().BeSameAs(daVersao["MODALIDADE"]);
        congelados["FAIXA_ETARIA"].ClassificacaoProtecao.Should().Be("PESSOAL");
    }

    [Fact(DisplayName = "O derivado que a versão vigente congelou continua congelado ainda que o catálogo de hoje não o traga como derivado")]
    public void Resolver_DerivadoDesativadoNoCatalogoPermaneceNaVersao()
    {
        Dictionary<string, FatoDerivadoCongelado> daVersao = new(StringComparer.Ordinal)
        {
            ["FAIXA_ETARIA"] = new("FAIXA_ETARIA", "Faixa etária", "NUMERICO", "ESCALAR", "PESSOAL", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Finalidade congelada."),
        };

        IReadOnlyDictionary<string, FatoDerivadoCongelado> congelados = CongelamentoDosFatosDerivados.Resolver([Derivado("MODALIDADE", "INTERNO")], daVersao);

        congelados.Keys.Should().BeEquivalentTo("FAIXA_ETARIA", "MODALIDADE");
        congelados["FAIXA_ETARIA"].Should().BeSameAs(daVersao["FAIXA_ETARIA"]);
    }

    [Fact(DisplayName = "A conferência das exigências usa a classificação que a versão vigente congelou para o derivado, e não a do catálogo de hoje")]
    public async Task Montar_ClassificacaoDoDerivadoVemDaVersaoVigente()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Derivado congelado", out _);
        CongeladoNaVersaoVigente daVersao = new(
            new Dictionary<Guid, ExigenciaCongelada>(),
            new Dictionary<string, FatoDerivadoCongelado>(StringComparer.Ordinal)
            {
                ["MODALIDADE"] = new("MODALIDADE", "Modalidade", "CATEGORICO", "MULTIVALORADO", "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", "Finalidade."),
            });

        ProtecaoVigenteDoProcesso protecao = await ProtecaoVigenteDasExigenciasDoProcesso.MontarAsync(
            processo, Substitute.For<ITipoDocumentoReader>(), [Derivado("MODALIDADE", "SENSIVEL")], daVersao, CancellationToken.None);

        protecao.Exigencias.ClassificacaoDosFatosDerivados["MODALIDADE"].Should().Be(ClassificacaoDeProtecao.Interno);
        protecao.FatosDerivados["MODALIDADE"].ClassificacaoProtecao.Should().Be("INTERNO");
    }
}
