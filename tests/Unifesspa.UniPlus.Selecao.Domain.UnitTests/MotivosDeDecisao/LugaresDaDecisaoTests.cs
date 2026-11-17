namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.MotivosDeDecisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

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

    [Fact(DisplayName = "Os lugares ausentes do retrato posterior são todos os que o retrato anterior tinha, fases e etapas")]
    public void AusentesEm_TodosOsQueSaem()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugaresDaDecisao semNada = LugaresDaDecisao.Do(ProcessoParaMotivos.SemFasesNemEtapas());

        LugarDaDecisao[] ausentes = [.. LugaresDaDecisao.Do(processo).AusentesEm(semNada)];

        ausentes.Should().BeEquivalentTo([
            ProcessoParaMotivos.LugarDaFase(processo, "INSCRICAO"),
            ProcessoParaMotivos.LugarDaFase(processo, "HOMOLOGACAO_INSCRICOES"),
            ProcessoParaMotivos.LugarDaEtapa(processo, "Prova objetiva"),
            ProcessoParaMotivos.LugarDaEtapa(processo, "Prova discursiva"),
        ]);
    }

    [Fact(DisplayName = "Só a etapa devolvida sem a identidade anterior aparece como ausente")]
    public void AusentesEm_SoOQueSaiu()
    {
        ProcessoSeletivo processo = ProcessoParaMotivos.Rascunho();
        LugarDaDecisao discursiva = ProcessoParaMotivos.LugarDaEtapa(processo, "Prova discursiva");
        LugaresDaDecisao antes = LugaresDaDecisao.Do(processo);
        EtapaProcesso mantida = processo.Etapas.First();
        processo.DefinirEtapas([mantida]).IsSuccess.Should().BeTrue();

        antes.AusentesEm(LugaresDaDecisao.Do(processo)).Should().Equal(discursiva);
    }
}
