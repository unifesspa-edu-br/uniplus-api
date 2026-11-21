namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class TipoMotivoDeDecisaoCodigosTests
{
    [Fact(DisplayName = "Cada tipo de decisão tem o código que o contrato publica")]
    public void Codigo_DeCadaTipo_EOQueOContratoPublica() =>
        Enum.GetValues<TipoMotivoDeDecisao>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<TipoMotivoDeDecisao, string>
            {
                [TipoMotivoDeDecisao.IsencaoDaTaxa] = "ISENCAO_DA_TAXA",
                [TipoMotivoDeDecisao.HomologacaoDasInscricoes] = "HOMOLOGACAO_DAS_INSCRICOES",
                [TipoMotivoDeDecisao.AnaliseDocumental] = "ANALISE_DOCUMENTAL",
                [TipoMotivoDeDecisao.Heteroidentificacao] = "HETEROIDENTIFICACAO",
                [TipoMotivoDeDecisao.AvaliacaoBiopsicossocial] = "AVALIACAO_BIOPSICOSSOCIAL",
                [TipoMotivoDeDecisao.Classificacao] = "CLASSIFICACAO",
                [TipoMotivoDeDecisao.GabaritoDaProva] = "GABARITO_DA_PROVA",
                [TipoMotivoDeDecisao.NotaDaEtapa] = "NOTA_DA_ETAPA",
            });

    [Fact(DisplayName = "Todo tipo de decisão tem rótulo")]
    public void Rotulo_DeCadaTipo_NaoEstaVazio() =>
        Enum.GetValues<TipoMotivoDeDecisao>().Should().OnlyContain(static v => !string.IsNullOrWhiteSpace(v.Rotulo()));

    [Fact(DisplayName = "O catálogo lê o tipo pelo código exato, e não reconhece caixa diferente, espaço nem texto ausente")]
    public void TentarObter_SoReconheceOCodigoExato()
    {
        CatalogoDe<TipoMotivoDeDecisao>.TentarObter("CLASSIFICACAO", out TipoMotivoDeDecisao tipo).Should().BeTrue();
        tipo.Should().Be(TipoMotivoDeDecisao.Classificacao);
        CatalogoDe<TipoMotivoDeDecisao>.TentarObter("classificacao", out _).Should().BeFalse();
        CatalogoDe<TipoMotivoDeDecisao>.TentarObter("CLASSIFICACAO ", out _).Should().BeFalse();
        CatalogoDe<TipoMotivoDeDecisao>.TentarObter("Classificacao", out _).Should().BeFalse();
        CatalogoDe<TipoMotivoDeDecisao>.TentarObter(null, out _).Should().BeFalse();
        CatalogoDe<TipoMotivoDeDecisao>.TentarObter(string.Empty, out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Os códigos dos tipos de decisão passam na conferência da subida")]
    public void ConferirEnumsDe_AssemblyDasRegras_Passa() =>
        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe([typeof(TipoMotivoDeDecisao).Assembly])).Should().NotThrow();
}
