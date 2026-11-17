namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As fases e etapas em que o processo decide, e o que saiu delas entre dois retratos do processo.</summary>
public sealed class LugaresDaDecisaoTests
{
    [Fact(DisplayName = "O processo reconhece as fases e as etapas que tem e não reconhece a que não tem")]
    public void Contem_SoOQueOProcessoTem()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugaresDaDecisao lugares = LugaresDaDecisao.Do(processo);

        lugares.Contem(ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO")).Should().BeTrue();
        lugares.Contem(ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva")).Should().BeTrue();
        lugares.Contem(LugarDaDecisao.DaFase(Guid.CreateVersion7())).Should().BeFalse();
        lugares.Contem(LugarDaDecisao.DaEtapa(Guid.CreateVersion7())).Should().BeFalse();
    }

    [Fact(DisplayName = "A fase e a etapa de mesma identidade são lugares diferentes")]
    public void Contem_FaseEEtapaNaoSeConfundem()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        Guid identidadeDaFase = processo.CronogramaFases.First().FaseCanonicaOrigemId;

        LugaresDaDecisao.Do(processo).Contem(LugarDaDecisao.DaEtapa(identidadeDaFase)).Should().BeFalse();
    }

    [Fact(DisplayName = "Os lugares ausentes do retrato posterior saem com as fases antes das etapas, cada uma na sua ordem, com o campo e o nome")]
    public void AusentesEm_FasesAntesDasEtapasNaOrdem()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugaresDaDecisao semNada = LugaresDaDecisao.Do(ProcessoParaMotivos.SemFasesNemEtapas());

        LugarNoProcesso[] ausentes = [.. LugaresDaDecisao.Do(processo).AusentesEm(semNada)];

        ausentes.Select(a => a.Campo).Should().Equal(
            "cronograma.fases[INSCRICAO]", "cronograma.fases[HOMOLOGACAO_INSCRICOES]",
            "cronograma.etapas[Prova objetiva]", "cronograma.etapas[Prova discursiva]");
        ausentes.Select(a => a.Descricao).Should().Equal(
            "A fase 'INSCRICAO'", "A fase 'HOMOLOGACAO_INSCRICOES'", "A etapa 'Prova objetiva'", "A etapa 'Prova discursiva'");
    }

    [Fact(DisplayName = "Só a etapa devolvida sem a identidade anterior aparece como ausente")]
    public void AusentesEm_SoOQueSaiu()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugaresDaDecisao antes = LugaresDaDecisao.Do(processo);
        EtapaProcesso mantida = processo.Etapas.First();
        processo.DefinirEtapas([mantida], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        antes.AusentesEm(LugaresDaDecisao.Do(processo)).Select(a => a.Campo).Should().Equal("cronograma.etapas[Prova discursiva]");
    }
}
