namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using static FaseDaRetificacaoDeTeste;

/// <summary>
/// Início e conclusão de fase têm uma definição só (UNI-REQ-0211, UNI-REQ-0333): começou quando a execução a traz; concluiu
/// quando todos os definitivos dela foram publicados nela mesma ou, se ela não publica definitivo, quando o período dela terminou.
/// </summary>
public sealed class AndamentoDasFasesTests
{
    private const string Materia = "RESULTADO_PROVA";

    private static readonly TimeSpan UmMicrossegundo = TimeSpan.FromTicks(10);

    private static AndamentoDaFase Andamento(FaseCronograma fase, FatosDaExecucao fatos) =>
        new AndamentoDasFases([fase], [], fatos).Da(fase);

    private static (DateTimeOffset Inicio, DateTimeOffset Fim) PeriodoQueTerminaEm(DateTimeOffset fim) => (fim.AddDays(-5), fim);

    [Fact(DisplayName = "A fase que a execução não traz ainda não começou, mesmo com o período já terminado")]
    public void FaseNaoIniciada_PorComecar()
    {
        FaseCronograma fase = Fase(1, "PROVA", PeriodoQueTerminaEm(Instante.AddDays(-1)));

        Andamento(fase, Fatos(Iniciadas())).Should().Be(AndamentoDaFase.PorComecar);
    }

    [Fact(DisplayName = "A fase iniciada sem definitivo, com o período ainda correndo, está em andamento")]
    public void FaseSemDefinitivoComPeriodoCorrendo_EmAndamento()
    {
        FaseCronograma fase = Fase(1, "PROVA", PeriodoQueTerminaEm(Instante.AddDays(2)));

        Andamento(fase, Fatos(Iniciadas(fase))).Should().Be(AndamentoDaFase.EmAndamento);
    }

    [Fact(DisplayName = "A fase iniciada sem definitivo conclui depois do fim do período, sem depender de fase seguinte")]
    public void FaseSemDefinitivoComPeriodoTerminado_Concluida()
    {
        FaseCronograma fase = Fase(1, "PROVA", PeriodoQueTerminaEm(Instante - UmMicrossegundo));

        Andamento(fase, Fatos(Iniciadas(fase))).Should().Be(AndamentoDaFase.Concluida);
    }

    [Fact(DisplayName = "No instante exato do fim o período ainda corre, e um microssegundo depois a fase está concluída")]
    public void FaseSemDefinitivo_FimInclusivo()
    {
        FaseCronograma noFim = Fase(1, "PROVA", PeriodoQueTerminaEm(Instante));
        FaseCronograma depoisDoFim = Fase(1, "PROVA", PeriodoQueTerminaEm(Instante - UmMicrossegundo));

        Andamento(noFim, Fatos(Iniciadas(noFim))).Should().Be(AndamentoDaFase.EmAndamento);
        Andamento(depoisDoFim, Fatos(Iniciadas(depoisDoFim))).Should().Be(AndamentoDaFase.Concluida);
    }

    [Fact(DisplayName = "A última fase, sem definitivo e com o período correndo, não conclui")]
    public void UltimaFaseSemDefinitivoComPeriodoCorrendo_NaoConclui()
    {
        FaseCronograma matricula = Fase(2, "MATRICULA", PeriodoQueTerminaEm(Instante.AddDays(30)));

        Andamento(matricula, Fatos(Iniciadas(matricula))).Should().Be(AndamentoDaFase.EmAndamento);
    }

    [Fact(DisplayName = "A fase sem definitivo cujo período já terminou volta a correr quando a versão nova prorroga o fim")]
    public void FaseSemDefinitivo_ProrrogacaoDoFim_VoltaACorrer()
    {
        FaseCronograma publicada = Fase(1, "PROVA", PeriodoQueTerminaEm(Instante.AddDays(-1)));
        FaseCronograma prorrogada = ComJanela(publicada, publicada.Inicio, Instante.AddDays(3));

        Andamento(publicada, Fatos(Iniciadas(publicada))).Should().Be(AndamentoDaFase.Concluida);
        Andamento(prorrogada, Fatos(Iniciadas(publicada))).Should().Be(AndamentoDaFase.EmAndamento);
    }

    [Fact(DisplayName = "A fase sem data final não conclui pelo período: sem fim não há como o período terminar")]
    public void FaseSemDefinitivoSemFim_NaoConclui()
    {
        FaseCronograma semFim = SemDataFinal(1, "PROVA", Instante.AddDays(-10));

        Andamento(semFim, Fatos(Iniciadas(semFim))).Should().Be(AndamentoDaFase.EmAndamento);
    }

    [Fact(DisplayName = "A fase iniciada pela execução, sem início declarado, conclui depois do fim declarado")]
    public void FaseSemInicioDeclarado_IniciadaPelaExecucao_ConcluiDepoisDoFim()
    {
        FaseCronograma publicada = SemDataFinal(1, "PROVA", Instante.AddDays(-10));
        FaseCronograma soComFim = ComJanela(publicada, null, Instante - UmMicrossegundo);
        FaseCronograma fimQueNaoPassou = ComJanela(publicada, null, Instante);

        Andamento(soComFim, Fatos(Iniciadas(soComFim))).Should().Be(AndamentoDaFase.Concluida);
        Andamento(fimQueNaoPassou, Fatos(Iniciadas(fimQueNaoPassou))).Should().Be(AndamentoDaFase.EmAndamento);
    }

    [Fact(DisplayName = "A fase com definitivo declarado e não publicado não conclui, mesmo com o período terminado")]
    public void DefinitivoNaoPublicado_NaoConclui()
    {
        FaseCronograma fase = Fase(1, "PROVA", PeriodoQueTerminaEm(Instante.AddDays(-1)), produtos: [Preliminar(Materia), Definitivo(Materia)]);

        Andamento(fase, Fatos(Iniciadas(fase))).Should().Be(AndamentoDaFase.EmAndamento);
    }

    [Fact(DisplayName = "O definitivo publicado conclui a fase que o declara, ainda que o período não tenha terminado")]
    public void DefinitivoPublicado_ConcluiAFase()
    {
        ProdutoDaFase definitivo = Definitivo(Materia);
        FaseCronograma fase = Fase(1, "PROVA", PeriodoQueTerminaEm(Instante.AddDays(3)), produtos: [Preliminar(Materia), definitivo]);

        Andamento(fase, Fatos(Iniciadas(fase), Item(fase, definitivo))).Should().Be(AndamentoDaFase.Concluida);
    }

    [Fact(DisplayName = "A fase seguinte iniciada não conclui a anterior: só o definitivo ou o fim do período concluem")]
    public void FaseSeguinteIniciada_NaoConcluiAAnterior()
    {
        FaseCronograma prova = Fase(1, "PROVA", PeriodoQueTerminaEm(Instante.AddDays(2)));
        FaseCronograma resultado = Fase(2, "RESULTADO", PeriodoQueTerminaEm(Instante.AddDays(4)));

        new AndamentoDasFases([prova, resultado], [], Fatos(Iniciadas(prova, resultado))).Da(prova).Should().Be(AndamentoDaFase.EmAndamento);
    }

    [Fact(DisplayName = "A janela de isenção ordenada antes da inscrição tem o momento dela e não conclui a inscrição")]
    public void IsencaoAntesDaInscricao_NaoConcluiAInscricao()
    {
        FaseCronograma isencao = Fase(1, "ISENCAO", coletaIsencao: true);
        FaseCronograma inscricao = Fase(2, "INSCRICAO", coletaInscricao: true);

        new AndamentoDasFases([isencao, inscricao], [], Fatos(Iniciadas(inscricao, isencao))).Da(inscricao).Should().Be(AndamentoDaFase.EmAndamento);
    }

    [Fact(DisplayName = "Duas definições de matéria exigem os dois definitivos publicados para concluir")]
    public void DoisDefinitivos_ExigemOsDoisPublicados()
    {
        ProdutoDaFase primeiro = Definitivo(Materia);
        ProdutoDaFase segundo = Definitivo("GABARITO");
        FaseCronograma fase = Fase(1, "PROVA", produtos: [primeiro, segundo]);

        Andamento(fase, Fatos(Iniciadas(fase), Item(fase, primeiro))).Should().Be(AndamentoDaFase.EmAndamento);
        Andamento(fase, Fatos(Iniciadas(fase), Item(fase, primeiro), Item(fase, segundo))).Should().Be(AndamentoDaFase.Concluida);
    }
}
