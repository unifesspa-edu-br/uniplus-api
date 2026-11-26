namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;
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

    [Fact]
    public void Adotar_ItensComChaveDeclarada_ConservaAChaveQueNaoVemDoRotulo()
    {
        ModeloDeEtapa modelo = new("Prova") { Itens = [new(null, "Redação", 5m), new(null, "Argumentação", 5m)] };

        EtapaProcesso etapa = modelo.Adotada();

        etapa.ItensDeAvaliacao.Select(static item => item.Chave.Valor).Should().Equal("item_1", "item_2");
        etapa.ItensDeAvaliacao.Select(static item => item.Rotulo).Should().Equal("Redação", "Argumentação");
    }

    [Fact]
    public void Adotar_ItensEmOutraOrdem_ConservaAChaveDeCadaUmEOrdemPelaPosicao()
    {
        ModeloDeEtapa modelo = new("Prova");
        ItemDeAvaliacao segundo = ItemDeAvaliacao.Criar(ChaveLocal.Criar("item_segundo").Value, new(null, "Coesão", 4m));
        ItemDeAvaliacao primeiro = ItemDeAvaliacao.Criar(ChaveLocal.Criar("item_primeiro").Value, new(null, "Coerência", 6m));

        EtapaProcesso etapa = Adotar(modelo, modelo.Avaliacao(), segundo, primeiro);

        etapa.ItensDeAvaliacao.Select(static item => (item.Chave.Valor, item.Ordem)).Should().Equal(("item_segundo", 1), ("item_primeiro", 2));
    }

    [Fact]
    public void Adotar_ItemDeMesmoRotuloEmEtapaDiferente_NaoRecebeChaveNova()
    {
        ModeloDeEtapa modelo = new("Prova");
        ItemDeAvaliacao item = ItemDeAvaliacao.Criar(ChaveLocal.Criar("criterio_unico").Value, new(null, "Redação", 10m));

        EtapaProcesso etapa = Adotar(modelo, modelo.Avaliacao(), item);

        etapa.ItensDeAvaliacao[0].Should().BeSameAs(item);
        etapa.ItensDeAvaliacao[0].EtapaProcessoId.Should().Be(etapa.Id);
    }

    [Fact]
    public void Adotar_NiveisComChaveDeclarada_ConservaAChaveEOrdenaPelaPosicao()
    {
        ModeloDeEtapa modelo = new("Prova");
        DeclaracaoDaAvaliacaoDaEtapa avaliacao = modelo.Avaliacao() with
        {
            EscalaDeAtendimento =
            [
                NivelDeAtendimento.Criar(ChaveLocal.Criar("nivel_zeta").Value, "Atendeu", 1m),
                NivelDeAtendimento.Criar(ChaveLocal.Criar("nivel_alfa").Value, "Não atendeu", 0m),
            ],
        };

        EtapaProcesso etapa = Adotar(modelo, avaliacao);

        etapa.EscalaDeAtendimento!.Select(static nivel => (nivel.Chave.Valor, nivel.Ordem)).Should().Equal(("nivel_zeta", 1), ("nivel_alfa", 2));
    }

    [Fact]
    public void AtualizarDados_ItemComChaveDeclarada_ContinuaComAChaveAoMudarORotulo()
    {
        ModeloDeEtapa modelo = new("Prova");
        ItemDeAvaliacao item = ItemDeAvaliacao.Criar(ChaveLocal.Criar("criterio_unico").Value, new(null, "Redação", 10m));
        EtapaProcesso etapa = Adotar(modelo, modelo.Avaliacao(), item);

        etapa.AtualizarDados(modelo.Identificacao(), modelo.Avaliacao(), [new(item.Id, "Texto dissertativo", 10m)]).IsSuccess.Should().BeTrue();

        etapa.ItensDeAvaliacao[0].Chave.Valor.Should().Be("criterio_unico");
    }

    private static EtapaProcesso Adotar(ModeloDeEtapa modelo, DeclaracaoDaAvaliacaoDaEtapa avaliacao, params ItemDeAvaliacao[] itens) =>
        EtapaProcesso.Adotar(
            ChaveLocal.Criar("etapa").Value, modelo.Identificacao(), avaliacao, itens,
            new JanelaDeTempo(null, null), new PublicacoesDaEtapa([], false, [], null), []);
}
