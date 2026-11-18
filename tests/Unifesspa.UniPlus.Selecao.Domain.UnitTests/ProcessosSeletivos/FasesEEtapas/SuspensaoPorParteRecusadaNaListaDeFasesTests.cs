namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;

/// <summary>
/// A leitura que recusou fase do documento deixa a lista de fases sem as que o autor escreveu: o que a regra afirma pela existência
/// ou pela quantidade de fases fica sem julgar, e o item do painel fica pendente. O que cada fase adotada diz sobre si e sobre as
/// outras segue julgado.
/// </summary>
public sealed class SuspensaoPorParteRecusadaNaListaDeFasesTests
{
    private const string FasesRecusadas = "fases";

    private static FaseCronograma FaseQueNaoColetaNemProduzResultado(string codigo, int ordem) =>
        new ModeloDeFase(codigo, ordem).Janela(ordem, ordem + 1).Adotada();

    private static string?[] CamposDasRecusas(CronogramaDoProcesso cronograma) =>
        [.. cronograma.ConferirEstrutura().Select(static recusa => recusa.Field)];

    [Fact(DisplayName = "O cronograma sem fases é recusado quando a leitura não recusou fase")]
    public void CronogramaVazio_SemParteRecusada_Recusa()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([]);

        cronograma.ConferirEstrutura().Select(static recusa => recusa.Error.Code).Should().Equal("ProcessoSeletivo.CronogramaFasesVazio");
        cronograma.ItemDaExistenciaDeFases().Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O cronograma sem fases não é recusado quando a leitura recusou fase, e o item da existência fica pendente")]
    public void CronogramaVazio_ComFaseRecusada_SuspendeEPendenta()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([], ComPartesRecusadas(FasesRecusadas));

        cronograma.ConferirEstrutura().Should().BeEmpty();
        cronograma.ItemDaExistenciaDeFases().Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "O item da existência de fases fica aprovado quando a fase recusada deixou outra fase adotada")]
    public void ItemDaExistencia_ComFaseRecusadaEFaseAdotada_FicaAprovado()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseQueNaoColetaNemProduzResultado("A", 1)], ComPartesRecusadas(FasesRecusadas));

        cronograma.ItemDaExistenciaDeFases().Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "A ordem repetida e a fase canônica repetida entre as fases adotadas continuam recusadas com fase recusada")]
    public void OrdemEFaseCanonicaRepetidas_ComFaseRecusada_ContinuamRecusadas()
    {
        FaseCronograma[] fases = [FaseQueNaoColetaNemProduzResultado("A", 1), FaseQueNaoColetaNemProduzResultado("A", 1)];

        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(fases, ComPartesRecusadas(FasesRecusadas));

        CamposDasRecusas(cronograma).Should().Equal("fases[1].ordem", "fases[1].faseCanonicaId");
        cronograma.ItemDasFasesEmConjunto().Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "A precedência e a sobreposição entre as fases adotadas continuam recusadas com fase recusada")]
    public void PrecedenciaESobreposicao_ComFaseRecusada_ContinuamRecusadas()
    {
        FaseCronograma[] fases =
        [
            new ModeloDeFase("A", 2).Janela(0, 10).Adotada(),
            new ModeloDeFase("B", 1).Janela(5, 20).Adotada(),
            new ModeloDeFase("C", 3).Janela(8, 30).Adotada(),
        ];
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(fases, ComPartesRecusadas(FasesRecusadas));

        cronograma.ConferirPrecedencia([new ArestaPrecedencia("A", "B", true), new ArestaPrecedencia("B", "C", false)])
            .Select(static recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
                ("fases[1].ordem", "ProcessoSeletivo.PrecedenciaFaseViolada"),
                ("fases[2].inicio", "ProcessoSeletivo.SobreposicaoDeJanelasNaoPermitida"));
    }

    [Fact(DisplayName = "A inscrição própria sem fase que coleta inscrição é recusada quando a leitura não recusou fase")]
    public void InscricaoPropriaSemColeta_SemParteRecusada_Recusa()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseQueNaoColetaNemProduzResultado("A", 1)]);

        cronograma.ConferirCronogramaExigidoPeloProcesso(OrigemCandidatos.InscricaoPropria, haVagasOfertadas: false)
            .Select(static recusa => recusa.Error.Code).Should().Equal("ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta");
        cronograma.ItemDaInscricaoPropria(OrigemCandidatos.InscricaoPropria).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "A inscrição própria sem fase que coleta inscrição não é recusada quando a leitura recusou fase, e o item fica pendente")]
    public void InscricaoPropriaSemColeta_ComFaseRecusada_SuspendeEPendenta()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseQueNaoColetaNemProduzResultado("A", 1)], ComPartesRecusadas(FasesRecusadas));

        cronograma.ConferirCronogramaExigidoPeloProcesso(OrigemCandidatos.InscricaoPropria, haVagasOfertadas: false).Should().BeEmpty();
        cronograma.ItemDaInscricaoPropria(OrigemCandidatos.InscricaoPropria).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "A origem que não exige coleta de inscrição mantém o item aprovado com fase recusada")]
    public void ImportacaoExterna_ComFaseRecusada_ItemSegueAprovado()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([FaseQueNaoColetaNemProduzResultado("A", 1)], ComPartesRecusadas(FasesRecusadas));

        cronograma.ItemDaInscricaoPropria(OrigemCandidatos.ImportacaoExterna).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "As vagas sem fase que produz resultado não são recusadas quando a leitura recusou fase, e o item fica pendente")]
    public void VagasSemFaseQueProduzResultado_ComFaseRecusada_SuspendeEPendenta()
    {
        FaseCronograma[] fases = [FaseQueNaoColetaNemProduzResultado("A", 1)];
        CronogramaDoProcesso lido = CronogramaDoProcesso.Do(fases);
        CronogramaDoProcesso comFaseRecusada = CronogramaDoProcesso.Do(fases, ComPartesRecusadas(FasesRecusadas));

        lido.ConferirCronogramaExigidoPeloProcesso(OrigemCandidatos.ImportacaoExterna, haVagasOfertadas: true).Should().ContainSingle();
        comFaseRecusada.ConferirCronogramaExigidoPeloProcesso(OrigemCandidatos.ImportacaoExterna, haVagasOfertadas: true).Should().BeEmpty();
        comFaseRecusada.ItemDasVagasOfertadas(haVagasOfertadas: true).Ok.Should().BeFalse();
        comFaseRecusada.ItemDasVagasOfertadas(haVagasOfertadas: false).Ok.Should().BeTrue();
    }

    [Fact(DisplayName = "A parte recusada dentro de uma fase não suspende a existência nem a quantidade de fases")]
    public void ParteRecusadaDentroDaFase_NaoSuspendeAsRegrasDeExistencia()
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(
            [FaseQueNaoColetaNemProduzResultado("A", 1)], ComPartesRecusadas("fases[0].unidadeResponsavel"));

        cronograma.TemParteRecusadaNaListaDeFases.Should().BeFalse();
        cronograma.ConferirCronogramaExigidoPeloProcesso(OrigemCandidatos.InscricaoPropria, haVagasOfertadas: true)
            .Select(static recusa => recusa.Error.Code).Should().Equal(
                "ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta", "ProcessoSeletivo.VagasSemFaseQueProduzResultado");
        CronogramaDoProcesso.Do([], ComPartesRecusadas("fases[0].unidadeResponsavel")).ConferirEstrutura().Should().ContainSingle();
    }
}
