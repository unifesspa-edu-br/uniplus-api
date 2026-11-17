namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.DeclaracoesAdotadasDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.CondicoesDeHomologacaoParaTestes;

/// <summary>O aviso de que uma fase homologa toda inscrição enviada: nunca bloqueia, só pede confirmação.</summary>
public sealed class AvisosDaHomologacaoTests
{
    private static ModeloDeFase Vazia(string codigo) =>
        new ModeloDeFase(codigo, 1).Homologando(RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO_HOMOLOGACAO", []));

    private static ModeloDeFase ComConfirmacao(string codigo) =>
        new ModeloDeFase(codigo, 1).Homologando(
            RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO_HOMOLOGACAO", [Alternativa(ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP"))]));

    private static CronogramaDoProcesso Cronograma(params ModeloDeFase[] fases) =>
        CronogramaDoProcesso.Do([.. fases.Select(static fase => fase.Adotada())]);

    [Fact(DisplayName = "Regra sem condição gera um aviso de cronograma que nomeia a fase")]
    public void RegraVazia_GeraAviso()
    {
        IReadOnlyList<AvisoConformidade> avisos = AvisosDaHomologacao.Da(Cronograma(Vazia("HOMOLOGACAO_INSCRICOES")));

        AvisoConformidade aviso = avisos.Should().ContainSingle().Subject;
        aviso.Codigo.Should().Be(CodigosDeAvisoDeConformidade.HomologacaoDasInscricoesSemCondicao);
        aviso.Dimensao.Should().Be(DimensaoConformidade.Cronograma);
        aviso.Mensagem.Should().Contain("HOMOLOGACAO_INSCRICOES");
    }

    [Fact(DisplayName = "Regra com condição não gera aviso")]
    public void RegraComCondicao_SemAviso() =>
        AvisosDaHomologacao.Da(Cronograma(ComConfirmacao("HOMOLOGACAO_INSCRICOES"))).Should().BeEmpty();

    [Fact(DisplayName = "A fase que não tem regra não gera aviso")]
    public void FaseSemRegra_SemAviso() =>
        AvisosDaHomologacao.Da(Cronograma(new ModeloDeFase("CLASSIFICACAO", 1))).Should().BeEmpty();

    [Fact(DisplayName = "Cada fase com regra vazia gera o seu aviso, na ordem do cronograma, e a fase com condição não")]
    public void VariasFases_UmAvisoPorFaseVazia()
    {
        IReadOnlyList<AvisoConformidade> avisos = AvisosDaHomologacao.Da(
            Cronograma(Vazia("FASE_B"), ComConfirmacao("FASE_C"), Vazia("FASE_A")));

        avisos.Should().HaveCount(2);
        avisos[0].Mensagem.Should().Contain("FASE_B");
        avisos[1].Mensagem.Should().Contain("FASE_A");
    }

    [Fact(DisplayName = "A regra a que a leitura recusou alguma alternativa não é dada como vazia: sem aviso")]
    public void RegraComAlternativaRecusada_SemAviso()
    {
        CronogramaDoProcesso lido = CronogramaDoProcesso.Do(
            [Vazia("HOMOLOGACAO_INSCRICOES").Adotada()], ComPartesRecusadas("fases[0].regraDeHomologacao"));

        AvisosDaHomologacao.Da(lido).Should().BeEmpty();
    }
}
