namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioProcessoSeletivoCronograma;

/// <summary>
/// A gravação das etapas acumula as causas de todos os assuntos (ADR-0125): o conjunto, a fase que a etapa declara, a janela
/// dentro da fase e o fechamento do ciclo. A janela de uma etapa só se confere quando a fase dela existe, e a fase inexistente de uma
/// etapa não impede avaliar a das outras.
/// </summary>
public sealed class RecusasAcumuladasNaGravacaoDasEtapasTests
{
    private const string FaseDaAvaliacao = "AVALIACAO";
    private const string EtapaSemFase = "ProcessoSeletivo.EtapaSemFaseNoCronograma";
    private const string EtapaComecaAntesDaFase = "ProcessoSeletivo.EtapaComecaAntesDaFase";
    private const string EtapaForaDaJanela = "ProcessoSeletivo.EtapaForaDaJanelaDaFase";
    private const string OrdemRepetida = "ProcessoSeletivo.OrdemEtapaDuplicada";

    private static ProcessoSeletivo ProcessoComFaseDeAvaliacao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirCronogramaFases(
            [new ModeloDeFase(FaseDaAvaliacao, 1).Janela(0, 10).Criar()], []).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static ModeloDeEtapa Etapa(string nome, int ordem, string faseCodigo) => new(nome) { Ordem = ordem, FaseCodigo = faseCodigo };

    private static (string? Campo, string Codigo)[] Causas(Result resultado) =>
        [.. resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code))];

    [Fact(DisplayName = "Etapas que declaram fase fora do cronograma: uma causa por etapa, na posição de cada uma")]
    public void Definir_FaseInexistenteEmDuasEtapas_DuasCausas()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeAvaliacao();
        EtapaProcesso[] etapas = [Etapa("Prova", 1, "FASE_A").Criar(), Etapa("Redação", 2, "FASE_B").Criar()];

        Result resultado = processo.DefinirEtapas(etapas);

        Causas(resultado).Should().Equal(("etapas[0].faseCodigo", EtapaSemFase), ("etapas[1].faseCodigo", EtapaSemFase));
    }

    [Fact(DisplayName = "A fase inexistente da etapa impede avaliar a janela dela, e a janela fora da fase de outra etapa continua sendo recusada")]
    public void Definir_FaseInexistenteImpedeAvaliarAJanelaDaMesmaEtapa()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeAvaliacao();
        DateTimeOffset foraDaFase = ModeloDeFase.Abertura.AddDays(-5);
        ModeloDeEtapa semFase = Etapa("Prova", 1, "FASE_INEXISTENTE") with { Inicio = foraDaFase, Fim = foraDaFase.AddDays(1) };
        ModeloDeEtapa foraDaJanela = Etapa("Redação", 2, FaseDaAvaliacao) with { Inicio = foraDaFase, Fim = foraDaFase.AddDays(1) };

        Result resultado = processo.DefinirEtapas([semFase.Criar(), foraDaJanela.Criar()]);

        Causas(resultado).Select(static causa => causa.Codigo).Should().Equal(EtapaSemFase, EtapaComecaAntesDaFase, EtapaForaDaJanela);
        resultado.Errors.Skip(1).Should().OnlyContain(static causa => causa.Field!.StartsWith("etapas[1]", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O conjunto e a fase inexistente acumulam: a ordem repetida e a fase inexistente saem juntas, o conjunto primeiro")]
    public void Definir_ConjuntoEFaseInexistente_Acumulam()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeAvaliacao();
        EtapaProcesso[] etapas = [Etapa("Prova", 1, FaseDaAvaliacao).Criar(), Etapa("Redação", 1, "FASE_INEXISTENTE").Criar()];

        Result resultado = processo.DefinirEtapas(etapas);

        Causas(resultado).Select(static causa => causa.Codigo).Should().Equal(OrdemRepetida, EtapaSemFase);
    }

    [Fact(DisplayName = "As etapas sem causa são gravadas")]
    public void Definir_SemCausas_Grava()
    {
        ProcessoSeletivo processo = ProcessoComFaseDeAvaliacao();

        Result resultado = processo.DefinirEtapas([Etapa("Prova", 1, FaseDaAvaliacao).Criar()]);

        resultado.IsSuccess.Should().BeTrue();
        processo.Etapas.Should().ContainSingle();
    }
}
