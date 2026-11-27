namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static ModeloDeFase;

public sealed class CompletudeDoCronogramaTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static (string? Campo, string Codigo)[] Completude(params FaseDeclarada[] fases) =>
        [.. Cronograma(fases).ConferirCompletude(Utc).Select(static r => (r.Field, r.Error.Code))];

    [Fact(DisplayName = "A fase que ancora a inscrição sem início ou sem fim é recusada no extremo que falta")]
    public void ConferirCompletude_FaseDeInscricaoSemJanela_RecusaNoExtremoAusente()
    {
        Completude(new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Publicando(Definitivo("RESULTADO")).Declarar())
            .Should().Equal(("fases[0].inicio", "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela"));
        Completude((new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Publicando(Definitivo("RESULTADO")).Janela(0, 5) with { Fim = null }).Declarar())
            .Should().Equal(("fases[0].fim", "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela"));
    }

    [Fact(DisplayName = "Só a fase de menor ordem entre as que coletam inscrição é conferida")]
    public void ConferirCompletude_SoAFaseAncora_EConferida()
    {
        Completude(
            new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Janela(0, 5).Declarar(),
            new ModeloDeFase("REMANEJAMENTO", 2) { ColetaInscricao = true }.Publicando(Definitivo("RESULTADO")).Declarar()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Toda fase sem unidade responsável é recusada, na ordem das posições")]
    public void ConferirCompletude_FasesSemUnidade_RecusaCadaUmaNaPosicao()
    {
        Completude(
            new ModeloDeFase("A", 9) { ComUnidade = false }.Publicando(Definitivo("RESULTADO")).Declarar(),
            new ModeloDeFase("B", 1) { ComUnidade = false }.Publicando(Definitivo("RESULTADO")).Declarar(),
            new ModeloDeFase("C", 5).Publicando(Definitivo("RESULTADO")).Declarar())
            .Should().Equal(
                ("fases[0].unidadeResponsavel", "ProcessoSeletivo.FaseSemUnidadeResponsavel"),
                ("fases[1].unidadeResponsavel", "ProcessoSeletivo.FaseSemUnidadeResponsavel"));
    }

    [Fact(DisplayName = "Os assuntos saem na ordem: janela da inscrição, unidade e isenção")]
    public void ConferirCompletude_VariosAssuntos_SaemNaOrdemDosAssuntos()
    {
        Completude(
            new ModeloDeFase("ISENCAO", 2) { ColetaSolicitacaoIsencao = true, ComUnidade = false }.Janela(0, 1).Declarar(),
            new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Publicando(Definitivo("RESULTADO")).Declarar())
            .Select(static r => r.Codigo).Should().Equal(
                "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela",
                "ProcessoSeletivo.FaseSemUnidadeResponsavel",
                "ProcessoSeletivo.JanelaDeIsencaoMenorQueCincoDias");
    }

    [Fact(DisplayName = "Os itens da completude saem na ordem do checklist e ficam vermelhos só quando há causa")]
    public void ItensDaCompletude_SaemNaOrdemDoChecklist()
    {
        CronogramaDoProcesso conforme = Cronograma(new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Janela(0, 5).Declarar());
        CronogramaDoProcesso semUnidade = Cronograma(new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true, ComUnidade = false }.Janela(0, 5).Declarar());
        CronogramaDoProcesso semDataFinal = Cronograma(new ModeloDeFase("PROVA", 1).Declarar());

        conforme.ItensDaCompletude(Utc).Select(static i => (i.Codigo, i.Ok)).Should().Equal(
            ("cronograma_fase_que_coleta_inscricao_sem_janela", true),
            ("cronograma_fase_sem_unidade_responsavel", true),
            ("cronograma_fase_sem_definitivo_sem_data_final", true),
            ("cronograma_janela_de_isencao", true));
        semUnidade.ItensDaCompletude(Utc).Single(static i => !i.Ok).Codigo.Should().Be("cronograma_fase_sem_unidade_responsavel");
        semDataFinal.ItensDaCompletude(Utc).Single(static i => !i.Ok).Codigo.Should().Be("cronograma_fase_sem_definitivo_sem_data_final");
    }

    [Fact(DisplayName = "A fase que não publica definitivo, de data definida na execução e sem data final, é recusada no fim, com o nome da fase")]
    public void ConferirCompletude_FaseSemDefinitivoSemDataFinal_RecusaNoFim()
    {
        FaseDeclarada semDefinitivo = new ModeloDeFase("PROVA", 1) { Nome = "Prova escrita" }.Declarar();
        FaseDeclarada soComPreliminar = new ModeloDeFase("ANALISE", 2) { Inicio = ModeloDeFase.Abertura }.Publicando(Preliminar("RESULTADO")).Declarar();

        Cronograma(semDefinitivo, soComPreliminar).ConferirCompletude(Utc).Should().SatisfyRespectively(
            static primeira =>
            {
                primeira.Field.Should().Be("fases[0].fim");
                primeira.Error.Code.Should().Be("ProcessoSeletivo.FaseSemDefinitivoSemDataFinal");
                primeira.Error.Message.Should().Contain("'Prova escrita'").And.NotContain("PROVA");
            },
            static segunda => (segunda.Field, segunda.Error.Code).Should().Be(("fases[1].fim", "ProcessoSeletivo.FaseSemDefinitivoSemDataFinal")));
    }

    [Fact(DisplayName = "A fase cuja lista de produtos foi recusada na leitura não é recusada por falta de data final, e o item do painel fica pendente")]
    public void ConferirCompletude_ProdutosRecusadosSemDataFinal_SuspendeARecusaEMantemOItemPendente()
    {
        FaseDeclarada fase = ComProdutosRecusados(new ModeloDeFase("PROVA", 1));

        Completude(fase).Should().BeEmpty();
        ItemDaDataFinal(Cronograma(fase)).Ok.Should().BeFalse("a lista recusada não diz o que a fase publica, então a data final não pode ser dada como conforme");
    }

    [Fact(DisplayName = "Só a fase de produtos recusados fica suspensa: a outra fase sem data final é recusada e o item do painel continua pendente")]
    public void ConferirCompletude_ProdutosRecusadosEOutraFaseSemDataFinal_SoARecusadaNaoFicaSuspensa()
    {
        FaseDeclarada comProdutosRecusados = ComProdutosRecusados(new ModeloDeFase("PROVA", 1));
        FaseDeclarada independente = new ModeloDeFase("ANALISE", 2).Declarar();

        Completude(comProdutosRecusados, independente).Should().Equal(("fases[1].fim", "ProcessoSeletivo.FaseSemDefinitivoSemDataFinal"));
        ItemDaDataFinal(Cronograma(comProdutosRecusados, independente)).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "A fase sem definitivo com data final declarada, mesmo sem início, não é recusada")]
    public void ConferirCompletude_FaseSemDefinitivoComDataFinal_NaoRecusa()
    {
        Completude((new ModeloDeFase("PROVA", 1).Janela(0, 5) with { Inicio = null }).Declarar()).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase que publica o definitivo, ou de data fixada no edital, não é julgada por falta de data final")]
    public void ConferirCompletude_FaseQueNaoPrecisaDaDataFinal_NaoRecusa()
    {
        Completude(new ModeloDeFase("RESULTADO", 1).Publicando(Definitivo("RESULTADO")).Declarar()).Should().BeEmpty();
        Completude((new ModeloDeFase("PROVA", 1) { OrigemData = OrigemDataFase.NoEdital }).Declarar()).Should().BeEmpty();
    }

    private static FaseDeclarada ComProdutosRecusados(ModeloDeFase modelo) => FaseDeclarada.Declarar(
        Guid.CreateVersion7(), modelo.Origem(), modelo.Decisoes(), modelo.Publicacoes(), modelo.Bancas,
        new HashSet<string> { FaseDeclarada.ListaDosProdutos });

    private static ItemConformidade ItemDaDataFinal(CronogramaDoProcesso cronograma) =>
        cronograma.ItensDaCompletude(Utc).Single(static i => i.Codigo == "cronograma_fase_sem_definitivo_sem_data_final");
}
