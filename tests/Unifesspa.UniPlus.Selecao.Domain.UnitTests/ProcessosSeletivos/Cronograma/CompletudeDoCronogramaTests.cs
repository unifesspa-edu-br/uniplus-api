namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

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
        Completude(new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Declarar())
            .Should().Equal(("fases[0].inicio", "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela"));
        Completude((new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Janela(0, 5) with { Fim = null }).Declarar())
            .Should().Equal(("fases[0].fim", "ProcessoSeletivo.FaseQueColetaInscricaoSemJanela"));
    }

    [Fact(DisplayName = "Só a fase de menor ordem entre as que coletam inscrição é conferida")]
    public void ConferirCompletude_SoAFaseAncora_EConferida()
    {
        Completude(
            new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Janela(0, 5).Declarar(),
            new ModeloDeFase("REMANEJAMENTO", 2) { ColetaInscricao = true }.Declarar()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Toda fase sem unidade responsável é recusada, na ordem das posições")]
    public void ConferirCompletude_FasesSemUnidade_RecusaCadaUmaNaPosicao()
    {
        Completude(
            new ModeloDeFase("A", 9) { ComUnidade = false }.Declarar(),
            new ModeloDeFase("B", 1) { ComUnidade = false }.Declarar(),
            new ModeloDeFase("C", 5).Declarar())
            .Should().Equal(
                ("fases[0].unidadeResponsavel", "ProcessoSeletivo.FaseSemUnidadeResponsavel"),
                ("fases[1].unidadeResponsavel", "ProcessoSeletivo.FaseSemUnidadeResponsavel"));
    }

    [Fact(DisplayName = "Os assuntos saem na ordem: janela da inscrição, unidade e isenção")]
    public void ConferirCompletude_VariosAssuntos_SaemNaOrdemDosAssuntos()
    {
        Completude(
            new ModeloDeFase("ISENCAO", 2) { ColetaSolicitacaoIsencao = true, ComUnidade = false }.Janela(0, 1).Declarar(),
            new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Declarar())
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

        conforme.ItensDaCompletude(Utc).Select(static i => (i.Codigo, i.Ok)).Should().Equal(
            ("cronograma_fase_que_coleta_inscricao_sem_janela", true),
            ("cronograma_fase_sem_unidade_responsavel", true),
            ("cronograma_janela_de_isencao", true));
        semUnidade.ItensDaCompletude(Utc).Single(static i => !i.Ok).Codigo.Should().Be("cronograma_fase_sem_unidade_responsavel");
    }
}
