namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Recursos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>A sugestão nasce da forma de avaliar da janela; quem publica pode declarar outra.</summary>
public sealed class SugestaoDoProcedimentoDeRecursoTests
{
    private static readonly AlvoDoProcedimentoDeRecurso Item = AlvoDoProcedimentoDeRecurso.DoItem(Guid.NewGuid());
    private static readonly AlvoDoProcedimentoDeRecurso Area = AlvoDoProcedimentoDeRecurso.DaArea(AreaDoEnem.Matematica);

    private static string? Sugerir(TipoMotivoDeDecisao tipo, AlvoDoProcedimentoDeRecurso? alvo, OrigemDaNotaDaJanela origem) =>
        SugestaoDoProcedimentoDeRecurso.Para(tipo, alvo, origem);

    [Fact(DisplayName = "Item de etapa avaliada por pessoas sugere recorreção")]
    public void ItemDeEtapaHumana_SugereRecorrecao() =>
        Sugerir(TipoMotivoDeDecisao.AnaliseDocumental, Item, new(FormaDeAvaliacao.Humana, false))
            .Should().Be(RegraProcedimentoRecursoCodigo.Recorrecao);

    [Fact(DisplayName = "Item de etapa objetiva sugere recálculo")]
    public void ItemDeEtapaObjetiva_SugereRecalculo() =>
        Sugerir(TipoMotivoDeDecisao.AnaliseDocumental, Item, new(FormaDeAvaliacao.Objetiva, false))
            .Should().Be(RegraProcedimentoRecursoCodigo.Recalculo);

    [Fact(DisplayName = "Etapa de nota do ENEM sugere recálculo, qualquer que seja a forma declarada")]
    public void EtapaDeNotaDoEnem_SugereRecalculo() =>
        Sugerir(TipoMotivoDeDecisao.AnaliseDocumental, Item, new(FormaDeAvaliacao.Humana, true))
            .Should().Be(RegraProcedimentoRecursoCodigo.Recalculo);

    [Theory(DisplayName = "Alvo de área do ENEM sugere recálculo, com ou sem etapa")]
    [InlineData(false)]
    [InlineData(true)]
    public void AlvoDeArea_SugereRecalculo(bool daEtapa) =>
        Sugerir(TipoMotivoDeDecisao.AnaliseDocumental, Area, daEtapa ? new(FormaDeAvaliacao.Humana, true) : OrigemDaNotaDaJanela.DaFase)
            .Should().Be(RegraProcedimentoRecursoCodigo.Recalculo);

    [Fact(DisplayName = "Classificação sugere recálculo, porque a ordem é calculada")]
    public void Classificacao_SugereRecalculo() =>
        Sugerir(TipoMotivoDeDecisao.Classificacao, null, OrigemDaNotaDaJanela.DaFase)
            .Should().Be(RegraProcedimentoRecursoCodigo.Recalculo);

    [Fact(DisplayName = "Janela de fase sem alvo de área não tem base para sugerir")]
    public void FaseSemAlvoDeArea_NaoSugere() =>
        Sugerir(TipoMotivoDeDecisao.HomologacaoDasInscricoes, null, OrigemDaNotaDaJanela.DaFase).Should().BeNull();

    [Fact(DisplayName = "Item de etapa sem forma de avaliar declarada não tem base para sugerir")]
    public void ItemSemForma_NaoSugere() =>
        Sugerir(TipoMotivoDeDecisao.AnaliseDocumental, Item, new(null, false)).Should().BeNull();

    [Fact(DisplayName = "Item numa janela de fase não tem base para sugerir")]
    public void ItemNaFase_NaoSugere() =>
        Sugerir(TipoMotivoDeDecisao.AnaliseDocumental, Item, OrigemDaNotaDaJanela.DaFase).Should().BeNull();

    [Theory(DisplayName = "Gabarito da prova e nota da etapa não têm sugestão: quem publica declara o procedimento")]
    [InlineData(TipoMotivoDeDecisao.GabaritoDaProva, false)]
    [InlineData(TipoMotivoDeDecisao.GabaritoDaProva, true)]
    [InlineData(TipoMotivoDeDecisao.NotaDaEtapa, false)]
    [InlineData(TipoMotivoDeDecisao.NotaDaEtapa, true)]
    public void GabaritoENota_NaoSugerem(TipoMotivoDeDecisao tipo, bool alvoDeArea) =>
        Sugerir(tipo, alvoDeArea ? Area : Item, new(FormaDeAvaliacao.Humana, false)).Should().BeNull();
}
