namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes.FasesDeHomologacaoParaTestes;

/// <summary>
/// O que cada origem de candidatos exige da fase que homologa as inscrições. A fase é achada pela
/// capacidade que o edital configurou nela, que a conferência recebe pronta: o código canônico nunca é lido.
/// </summary>
public sealed class ExigenciaDaHomologacaoPorOrigemTests
{
    private static bool QualquerFaseHomologa(FaseCronograma fase) => true;

    private static bool SoAFaseHomologa(FaseCronograma escolhida, FaseCronograma fase) => fase == escolhida;

    [Fact(DisplayName = "Inscrição própria sem fase que homologa é recusada, mesmo com a fase de coleta")]
    public void InscricaoPropria_SemFaseQueHomologa_Recusa()
    {
        FaseCronograma[] fases = [FaseDeColeta(1), Fase(2, "CLASSIFICACAO")];

        DomainError? recusa = ExigenciaDaHomologacaoPorOrigem.Pendencia(
            OrigemCandidatos.InscricaoPropria, usaNotaImportadaDeExame: false, fases, static _ => false);

        recusa!.Code.Should().Be(ProcessoSeletivoErrorCodes.InscricaoPropriaSemHomologacaoDasInscricoes);
    }

    [Theory(DisplayName = "Inscrição própria com uma fase que homologa é aceita, qualquer que seja o código canônico dela")]
    [InlineData("HOMOLOGACAO_INSCRICOES")]
    [InlineData("CLASSIFICACAO")]
    [InlineData("ENSALAMENTO")]
    public void InscricaoPropria_ComFaseQueHomologa_Aceita(string codigo)
    {
        FaseCronograma homologa = Fase(2, codigo);

        DomainError? recusa = ExigenciaDaHomologacaoPorOrigem.Pendencia(
            OrigemCandidatos.InscricaoPropria, usaNotaImportadaDeExame: false, [FaseDeColeta(1), homologa], fase => SoAFaseHomologa(homologa, fase));

        recusa.Should().BeNull();
    }

    [Fact(DisplayName = "A fase HOMOLOGACAO_RESULTADO_FINAL não satisfaz a exigência só pelo nome: o que vale é a capacidade")]
    public void InscricaoPropria_FaseDoResultadoFinal_NaoSatisfazPeloNome()
    {
        FaseCronograma[] fases = [FaseDeColeta(1), Fase(2, "HOMOLOGACAO_RESULTADO_FINAL")];

        DomainError? recusa = ExigenciaDaHomologacaoPorOrigem.Pendencia(
            OrigemCandidatos.InscricaoPropria, usaNotaImportadaDeExame: false, fases, static fase => fase.Codigo == "HOMOLOGACAO_INSCRICOES");

        recusa!.Code.Should().Be(ProcessoSeletivoErrorCodes.InscricaoPropriaSemHomologacaoDasInscricoes);
    }

    [Fact(DisplayName = "Inscrição própria com nota importada de exame e sem fase que homologa é recusada com o código da importação de exame")]
    public void ImportacaoDeExame_SemFaseQueHomologa_RecusaComOCodigoDoExame()
    {
        DomainError? recusa = ExigenciaDaHomologacaoPorOrigem.Pendencia(
            OrigemCandidatos.InscricaoPropria, usaNotaImportadaDeExame: true, [FaseDeColeta(1)], static _ => false);

        recusa!.Code.Should().Be(ProcessoSeletivoErrorCodes.ImportacaoDeExameSemHomologacaoDasInscricoes);
    }

    [Fact(DisplayName = "Importação externa sem fase que homologa é recusada sem exigir fase de coleta")]
    public void ImportacaoExterna_SemFaseQueHomologa_Recusa()
    {
        DomainError? recusa = ExigenciaDaHomologacaoPorOrigem.Pendencia(
            OrigemCandidatos.ImportacaoExterna, usaNotaImportadaDeExame: false, [Fase(1, "CLASSIFICACAO")], static _ => false);

        recusa!.Code.Should().Be(ProcessoSeletivoErrorCodes.ImportacaoExternaSemHomologacaoDasInscricoes);
    }

    [Fact(DisplayName = "Importação externa com a fase que homologa, o preliminar, o definitivo e a janela de recurso é aceita")]
    public void ImportacaoExterna_Completa_Aceita()
    {
        ProdutoDaFase preliminar = Preliminar();
        FaseCronograma fase = Fase(1, "HOMOLOGACAO_INSCRICOES", [preliminar, Definitivo()], JanelaDeRecurso(preliminar.Id));

        DomainError? recusa = ExigenciaDaHomologacaoPorOrigem.Pendencia(
            OrigemCandidatos.ImportacaoExterna, usaNotaImportadaDeExame: false, [fase], QualquerFaseHomologa);

        recusa.Should().BeNull();
    }

    [Fact(DisplayName = "Importação externa com a fase que homologa mas sem a janela de recurso entre o preliminar e o definitivo é recusada")]
    public void ImportacaoExterna_SemJanelaDeRecurso_Recusa()
    {
        FaseCronograma fase = Fase(1, "HOMOLOGACAO_INSCRICOES", [Preliminar(), Definitivo()]);

        DomainError? recusa = ExigenciaDaHomologacaoPorOrigem.Pendencia(
            OrigemCandidatos.ImportacaoExterna, usaNotaImportadaDeExame: false, [fase], QualquerFaseHomologa);

        recusa!.Code.Should().Be(ProcessoSeletivoErrorCodes.ImportacaoExternaSemJanelaDeRecurso);
    }

    [Theory(DisplayName = "Importação externa cuja fase que homologa não declara o preliminar e o definitivo é recusada como resultado não declarado")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ImportacaoExterna_SemPreliminarOuDefinitivo_ResultadoNaoDeclarado(bool temPreliminar, bool temDefinitivo)
    {
        FaseCronograma fase = Fase(1, "HOMOLOGACAO_INSCRICOES", ProdutosDeclarados(temPreliminar, temDefinitivo));

        DomainError? recusa = ExigenciaDaHomologacaoPorOrigem.Pendencia(
            OrigemCandidatos.ImportacaoExterna, usaNotaImportadaDeExame: false, [fase], QualquerFaseHomologa);

        recusa!.Code.Should().Be(HomologacaoInscricaoErrorCodes.ResultadoNaoDeclarado);
    }

    private static List<ProdutoDaFase> ProdutosDeclarados(bool temPreliminar, bool temDefinitivo)
    {
        List<ProdutoDaFase> produtos = [];
        if (temPreliminar)
        {
            produtos.Add(Preliminar());
        }

        if (temDefinitivo)
        {
            produtos.Add(Definitivo());
        }

        return produtos;
    }

    [Fact(DisplayName = "Com várias fases que homologam, basta uma completa: a ordem das fases não muda o veredicto")]
    public void ImportacaoExterna_VariasFases_BastaUmaCompleta()
    {
        FaseCronograma incompleta = Fase(1, "HOMOLOGACAO_INSCRICOES", [Preliminar()]);
        ProdutoDaFase preliminar = Preliminar();
        FaseCronograma completa = Fase(2, "HOMOLOGACAO_INSCRICOES", [preliminar, Definitivo()], JanelaDeRecurso(preliminar.Id));

        ExigenciaDaHomologacaoPorOrigem.Pendencia(OrigemCandidatos.ImportacaoExterna, false, [incompleta, completa], QualquerFaseHomologa).Should().BeNull();
        ExigenciaDaHomologacaoPorOrigem.Pendencia(OrigemCandidatos.ImportacaoExterna, false, [completa, incompleta], QualquerFaseHomologa).Should().BeNull();
    }

    [Fact(DisplayName = "Com várias fases que homologam e nenhuma completa, a recusa é a da primeira fase na ordem do cronograma")]
    public void ImportacaoExterna_NenhumaCompleta_RecusaPelaPrimeiraNaOrdem()
    {
        FaseCronograma semDefinitivo = Fase(1, "HOMOLOGACAO_INSCRICOES", [Preliminar()]);
        FaseCronograma semJanela = Fase(2, "HOMOLOGACAO_INSCRICOES", [Preliminar(), Definitivo()]);

        DomainError? recusa = ExigenciaDaHomologacaoPorOrigem.Pendencia(
            OrigemCandidatos.ImportacaoExterna, usaNotaImportadaDeExame: false, [semJanela, semDefinitivo], QualquerFaseHomologa);

        recusa!.Code.Should().Be(HomologacaoInscricaoErrorCodes.ResultadoNaoDeclarado);
    }

    [Fact(DisplayName = "O preliminar de um resultado e o definitivo de outro não fecham o ciclo: falta o resultado declarado")]
    public void ImportacaoExterna_PreliminarEDefinitivoDeResultadosDiferentes_ResultadoNaoDeclarado()
    {
        ProdutoDaFase preliminar = Preliminar("RESULTADO_A");
        FaseCronograma fase = Fase(1, "HOMOLOGACAO_INSCRICOES", [preliminar, Definitivo("RESULTADO_B")], JanelaDeRecurso(preliminar.Id));

        DomainError? recusa = ExigenciaDaHomologacaoPorOrigem.Pendencia(
            OrigemCandidatos.ImportacaoExterna, usaNotaImportadaDeExame: false, [fase], QualquerFaseHomologa);

        recusa!.Code.Should().Be(HomologacaoInscricaoErrorCodes.ResultadoNaoDeclarado);
    }

    [Fact(DisplayName = "A janela de recurso ancorada no preliminar de um resultado que a fase não conclui não vale como janela entre o preliminar e o definitivo")]
    public void ImportacaoExterna_JanelaAncoradaEmResultadoSemDefinitivo_SemJanelaDeRecurso()
    {
        ProdutoDaFase preliminarSemDefinitivo = Preliminar("RESULTADO_A");
        FaseCronograma fase = Fase(1, "HOMOLOGACAO_INSCRICOES", [preliminarSemDefinitivo, Preliminar(), Definitivo()], JanelaDeRecurso(preliminarSemDefinitivo.Id));

        DomainError? recusa = ExigenciaDaHomologacaoPorOrigem.Pendencia(
            OrigemCandidatos.ImportacaoExterna, usaNotaImportadaDeExame: false, [fase], QualquerFaseHomologa);

        recusa!.Code.Should().Be(ProcessoSeletivoErrorCodes.ImportacaoExternaSemJanelaDeRecurso);
    }

    [Fact(DisplayName = "A recusa da fase ausente sai antes da recusa do resultado: sem fase não há resultado a conferir")]
    public void ImportacaoExterna_SemFaseNemResultado_RecusaPrimeiroAFase()
    {
        DomainError? recusa = ExigenciaDaHomologacaoPorOrigem.Pendencia(
            OrigemCandidatos.ImportacaoExterna, usaNotaImportadaDeExame: false, [], static _ => false);

        recusa!.Code.Should().Be(ProcessoSeletivoErrorCodes.ImportacaoExternaSemHomologacaoDasInscricoes);
    }
}
