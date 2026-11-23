namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ChavesDosItensENiveisTests
{
    [Fact]
    public void Reconciliar_ItensComBasesIguais_FormaChavesDistintas()
    {
        List<ItemDeAvaliacao> itens = ReconciliacaoDosItensDeAvaliacao.Reconciliar([], [
            new(null, "Redação", 5m), new(null, "Redacao!", 5m), new(null, "—", 1m)]);
        itens.Select(static item => item.Chave.Valor).Should().Equal("item_redacao", "item_redacao_2", "item");
    }

    [Fact]
    public void Reconciliar_ItemReconhecido_PreservaChaveAoRenomear()
    {
        ItemDeAvaliacao item = ReconciliacaoDosItensDeAvaliacao.Reconciliar([], [new(null, "Redação", 10m)])[0];
        List<ItemDeAvaliacao> itens = ReconciliacaoDosItensDeAvaliacao.Reconciliar([item], [new(item.Id, "Novo título", 10m)]);
        itens[0].Chave.Valor.Should().Be("item_redacao");
    }

    [Fact]
    public void Reconciliar_NiveisComBasesIguais_FormaChavesDistintas()
    {
        List<NivelDeAtendimento> niveis = ReconciliacaoDosNiveisDeAtendimento.Reconciliar([], [
            new("—", 1m), new("(*)", 0m), new("Atende parcialmente (50%)", 0.5m)]);
        niveis.Select(static nivel => nivel.Chave.Valor).Should().Equal("nivel", "nivel_2", "nivel_atende_parcialmente_50");
    }

    [Fact]
    public void Reconciliar_NivelReconhecido_PreservaChaveAoRenomear()
    {
        NivelDeAtendimento anterior = ReconciliacaoDosNiveisDeAtendimento.Reconciliar([], [new("Atende", 1m)])[0];
        List<NivelDeAtendimento> niveis = ReconciliacaoDosNiveisDeAtendimento.Reconciliar([anterior], [anterior with { Rotulo = "Completo" }]);
        niveis[0].Chave.Valor.Should().Be("nivel_atende");
        niveis[0].Should().BeSameAs(anterior);
    }

    [Fact]
    public void Reconciliar_ChaveDeNivelDesconhecida_FormaNova()
    {
        NivelDeAtendimento declarado = new("Completo", 1m) { Chave = ChaveLocal.Criar("nivel_desconhecido").Value };
        ReconciliacaoDosNiveisDeAtendimento.Reconciliar([], [declarado])[0].Chave.Valor.Should().Be("nivel_completo");
    }

    [Fact]
    public void Reidratar_Item_PreservaChaveInformada()
    {
        ChaveLocal chave = ChaveLocal.Criar("item_conservado").Value;
        ItemDeAvaliacao.Reidratar(chave, Guid.CreateVersion7(), "Redação", 10m).Chave.Should().Be(chave);
    }
    [Fact]
    public void Reidratar_EtapaComNivel_PreservaChaveDoNivel()
    {
        ModeloDeEtapa modelo = new("Prova");
        ChaveLocal chave = ChaveLocal.Criar("nivel_conservado").Value;
        DeclaracaoDaAvaliacaoDaEtapa avaliacao = modelo.Avaliacao() with
        {
            EscalaDeAtendimento = [new NivelDeAtendimento("Completo", 1m) { Chave = chave }],
        };
        EtapaProcesso etapa = EtapaProcesso.Reidratar(ChaveLocal.Criar("etapa").Value,
            Guid.CreateVersion7(), modelo.Identificacao(), avaliacao, []);
        etapa.EscalaDeAtendimento![0].Chave.Should().Be(chave);
    }

}
