namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class TipoDeBancaCodigosTests
{
    [Fact(DisplayName = "Cada tipo de banca tem o código que o contrato publica")]
    public void Codigo_DeCadaTipo_EOQueOContratoPublica() =>
        Enum.GetValues<TipoDeBanca>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<TipoDeBanca, string>
            {
                [TipoDeBanca.AnaliseDocumental] = "BANCA_ANALISE_DOCUMENTAL",
                [TipoDeBanca.Entrevista] = "BANCA_ENTREVISTA",
                [TipoDeBanca.CorrecaoDeRedacoes] = "BANCA_CORRECAO_REDACOES",
                [TipoDeBanca.AnaliseDeRecursos] = "BANCA_ANALISE_RECURSOS",
                [TipoDeBanca.Heteroidentificacao] = "BANCA_HETEROIDENTIFICACAO",
                [TipoDeBanca.AvaliacaoBiopsicossocial] = "BANCA_BIOPSICOSSOCIAL",
                [TipoDeBanca.HomologacaoDasInscricoes] = "BANCA_HOMOLOGACAO_INSCRICOES",
                [TipoDeBanca.ImportacaoDasNotasDoInep] = "BANCA_IMPORTACAO_NOTAS_INEP",
                [TipoDeBanca.ImportacaoDeCandidatos] = "BANCA_IMPORTACAO_CANDIDATOS",
                [TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia] = "BANCA_ANALISE_RECURSOS_SEGUNDA_INSTANCIA",
            });

    [Fact(DisplayName = "O vocabulário de tipos de banca é publicado na ordem de exibição")]
    public void Valores_SaemNaOrdemDeDeclaracao() =>
        CatalogoDe<TipoDeBanca>.Valores().Should().Equal(
            TipoDeBanca.AnaliseDocumental,
            TipoDeBanca.Entrevista,
            TipoDeBanca.CorrecaoDeRedacoes,
            TipoDeBanca.AnaliseDeRecursos,
            TipoDeBanca.Heteroidentificacao,
            TipoDeBanca.AvaliacaoBiopsicossocial,
            TipoDeBanca.HomologacaoDasInscricoes,
            TipoDeBanca.ImportacaoDasNotasDoInep,
            TipoDeBanca.ImportacaoDeCandidatos,
            TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia);

    [Fact(DisplayName = "Todo tipo de banca tem rótulo")]
    public void Rotulo_DeCadaTipo_NaoEstaVazio() =>
        Enum.GetValues<TipoDeBanca>().Should().OnlyContain(static v => !string.IsNullOrWhiteSpace(v.Rotulo()));

    [Fact(DisplayName = "O tipo de banca de análise de recursos de 2ª instância tem o rótulo que o contrato publica")]
    public void Rotulo_DoTipoDeSegundaInstancia_EOQueOContratoPublica() =>
        TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia.Rotulo().Should().Be("Banca de análise de recursos de 2ª instância");

    [Fact(DisplayName = "O catálogo lê o tipo de banca pelo código exato, e não reconhece caixa diferente, espaço nem texto ausente")]
    public void TentarObter_SoReconheceOCodigoExato()
    {
        CatalogoDe<TipoDeBanca>.TentarObter("BANCA_ENTREVISTA", out TipoDeBanca tipo).Should().BeTrue();
        tipo.Should().Be(TipoDeBanca.Entrevista);
        CatalogoDe<TipoDeBanca>.TentarObter("banca_entrevista", out _).Should().BeFalse();
        CatalogoDe<TipoDeBanca>.TentarObter("BANCA_ENTREVISTA ", out _).Should().BeFalse();
        CatalogoDe<TipoDeBanca>.TentarObter(null, out _).Should().BeFalse();
        CatalogoDe<TipoDeBanca>.TentarObter(string.Empty, out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Os códigos dos tipos de banca passam na conferência da subida")]
    public void ConferirEnumsDe_AssemblyDasRegras_Passa() =>
        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe([typeof(TipoDeBanca).Assembly])).Should().NotThrow();
}
