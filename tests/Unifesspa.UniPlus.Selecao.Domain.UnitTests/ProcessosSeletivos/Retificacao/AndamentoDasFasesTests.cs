namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using static FaseDaRetificacaoDeTeste;

/// <summary>
/// Início e conclusão de fase têm uma definição só (UNI-REQ-0211, UNI-REQ-0333): começou quando a execução a traz;
/// concluiu quando o definitivo dela foi publicado ou, sem definitivo, quando a fase seguinte começou.
/// </summary>
public sealed class AndamentoDasFasesTests
{
    private const string Materia = "RESULTADO_PROVA";

    private static AndamentoDaFase Andamento(FaseCronograma fase, FaseCronograma[] fases, FatosDaExecucao fatos) =>
        new AndamentoDasFases(fases, [], fatos).Da(fase);

    [Fact(DisplayName = "A fase que a execução não traz ainda não começou")]
    public void FaseNaoIniciada_PorComecar()
    {
        FaseCronograma fase = Fase(1, "PROVA");

        Andamento(fase, [fase], Fatos(Iniciadas())).Should().Be(AndamentoDaFase.PorComecar);
    }

    [Fact(DisplayName = "A fase iniciada sem definitivo e sem fase seguinte iniciada está em andamento")]
    public void FaseIniciadaSemSeguinte_EmAndamento()
    {
        FaseCronograma prova = Fase(1, "PROVA");
        FaseCronograma resultado = Fase(2, "RESULTADO");

        Andamento(prova, [prova, resultado], Fatos(Iniciadas(prova))).Should().Be(AndamentoDaFase.EmAndamento);
    }

    [Fact(DisplayName = "A fase sem definitivo conclui quando a fase seguinte começou")]
    public void FaseSemDefinitivo_ConcluiQuandoASeguinteComeca()
    {
        FaseCronograma prova = Fase(1, "PROVA");
        FaseCronograma resultado = Fase(2, "RESULTADO");

        Andamento(prova, [prova, resultado], Fatos(Iniciadas(prova, resultado))).Should().Be(AndamentoDaFase.Concluida);
    }

    [Fact(DisplayName = "A última fase, sem definitivo e sem seguinte, não conclui")]
    public void UltimaFaseSemDefinitivo_NaoConclui()
    {
        FaseCronograma matricula = Fase(2, "MATRICULA");

        Andamento(matricula, [matricula], Fatos(Iniciadas(matricula))).Should().Be(AndamentoDaFase.EmAndamento);
    }

    [Fact(DisplayName = "O definitivo publicado conclui a fase que o declara, ainda que a seguinte não tenha começado")]
    public void DefinitivoPublicado_ConcluiAFase()
    {
        ProdutoDaFase definitivo = Definitivo(Materia);
        FaseCronograma fase = Fase(1, "PROVA", produtos: [Preliminar(Materia), definitivo]);
        FaseCronograma seguinte = Fase(2, "RESULTADO");

        Andamento(fase, [fase, seguinte], Fatos(Iniciadas(fase), Item(fase, definitivo))).Should().Be(AndamentoDaFase.Concluida);
    }

    [Fact(DisplayName = "A fase com definitivo declarado e não publicado não conclui, mesmo com a seguinte iniciada")]
    public void DefinitivoNaoPublicado_NaoConclui()
    {
        FaseCronograma fase = Fase(1, "PROVA", produtos: [Preliminar(Materia), Definitivo(Materia)]);
        FaseCronograma seguinte = Fase(2, "RESULTADO");

        Andamento(fase, [fase, seguinte], Fatos(Iniciadas(fase, seguinte))).Should().Be(AndamentoDaFase.EmAndamento);
    }

    [Fact(DisplayName = "O definitivo publicado na fase concluinte conclui a fase que abriu o ciclo")]
    public void DefinitivoDaConcluinte_ConcluiAFaseDeOrigem()
    {
        ProdutoDaFase definitivo = Definitivo(Materia);
        FaseCronograma origem = Fase(1, "PROVA", faseConcluinteCodigo: "RESULTADO", produtos: [Preliminar(Materia)]);
        FaseCronograma concluinte = Fase(2, "RESULTADO", produtos: [definitivo]);

        Andamento(origem, [origem, concluinte], Fatos(Iniciadas(origem), Item(concluinte, definitivo))).Should().Be(AndamentoDaFase.Concluida);
        Andamento(origem, [origem, concluinte], Fatos(Iniciadas(origem, concluinte))).Should().Be(AndamentoDaFase.EmAndamento);
    }

    [Fact(DisplayName = "A janela de isenção ordenada antes da inscrição tem o momento dela e não conclui a inscrição")]
    public void IsencaoAntesDaInscricao_NaoConcluiAInscricao()
    {
        FaseCronograma isencao = Fase(1, "ISENCAO", coletaIsencao: true);
        FaseCronograma inscricao = Fase(2, "INSCRICAO", coletaInscricao: true);

        Andamento(inscricao, [isencao, inscricao], Fatos(Iniciadas(inscricao, isencao))).Should().Be(AndamentoDaFase.EmAndamento);
    }

    [Fact(DisplayName = "Duas definições de matéria exigem os dois definitivos publicados para concluir")]
    public void DoisDefinitivos_ExigemOsDoisPublicados()
    {
        ProdutoDaFase primeiro = Definitivo(Materia);
        ProdutoDaFase segundo = Definitivo("GABARITO");
        FaseCronograma fase = Fase(1, "PROVA", produtos: [primeiro, segundo]);

        Andamento(fase, [fase], Fatos(Iniciadas(fase), Item(fase, primeiro))).Should().Be(AndamentoDaFase.EmAndamento);
        Andamento(fase, [fase], Fatos(Iniciadas(fase), Item(fase, primeiro), Item(fase, segundo))).Should().Be(AndamentoDaFase.Concluida);
    }
}
