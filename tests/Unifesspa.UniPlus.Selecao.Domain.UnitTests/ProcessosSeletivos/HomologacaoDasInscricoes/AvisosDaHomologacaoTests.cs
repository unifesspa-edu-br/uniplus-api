namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.CondicoesDeHomologacaoParaTestes;

/// <summary>O aviso de que uma fase homologa toda inscrição enviada: nunca bloqueia, só pede confirmação.</summary>
public sealed class AvisosDaHomologacaoTests
{
    private static RegraDeHomologacaoDasInscricoes Vazia() =>
        RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO_HOMOLOGACAO", []);

    private static RegraDeHomologacaoDasInscricoes ComConfirmacao() =>
        RegraDeHomologacaoDasInscricoes.Declarar(
            "RESULTADO_HOMOLOGACAO", [Alternativa(ConfirmacaoDoInep(2026, "SEM_RESULTADO_INEP"))]);

    [Fact(DisplayName = "Regra sem condição gera um aviso de cronograma que nomeia a fase")]
    public void RegraVazia_GeraAviso()
    {
        IReadOnlyList<AvisoConformidade> avisos = AvisosDaHomologacao.Da([("HOMOLOGACAO_INSCRICOES", Vazia())], usaNotaImportadaDeExame: false);

        AvisoConformidade aviso = avisos.Should().ContainSingle().Subject;
        aviso.Codigo.Should().Be(CodigosDeAvisoDeConformidade.HomologacaoDasInscricoesSemCondicao);
        aviso.Dimensao.Should().Be(DimensaoConformidade.Cronograma);
        aviso.Mensagem.Should().Contain("HOMOLOGACAO_INSCRICOES");
    }

    [Fact(DisplayName = "Regra com condição não gera aviso")]
    public void RegraComCondicao_SemAviso()
    {
        AvisosDaHomologacao.Da([("HOMOLOGACAO_INSCRICOES", ComConfirmacao())], usaNotaImportadaDeExame: false).Should().BeEmpty();
    }

    [Fact(DisplayName = "Com nota importada de exame não há aviso: a regra vazia é recusada pela conferência")]
    public void ComExame_SemAviso()
    {
        AvisosDaHomologacao.Da([("HOMOLOGACAO_INSCRICOES", Vazia())], usaNotaImportadaDeExame: true).Should().BeEmpty();
    }

    [Fact(DisplayName = "Cada fase com regra vazia gera o seu aviso, na ordem recebida, e a fase com condição não")]
    public void VariasFases_UmAvisoPorFaseVazia()
    {
        IReadOnlyList<AvisoConformidade> avisos = AvisosDaHomologacao.Da(
            [("FASE_B", Vazia()), ("FASE_C", ComConfirmacao()), ("FASE_A", Vazia())], usaNotaImportadaDeExame: false);

        avisos.Select(static aviso => aviso.Mensagem).Should().HaveCount(2);
        avisos[0].Mensagem.Should().Contain("FASE_B");
        avisos[1].Mensagem.Should().Contain("FASE_A");
    }
}
